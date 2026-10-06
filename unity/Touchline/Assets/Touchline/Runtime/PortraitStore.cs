using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Touchline
{
    public sealed class PortraitStore : MonoBehaviour
    {
        const int CacheLimit=24,ConcurrentLimit=3;
        [Serializable] class Entry { public string player,file; }
        [Serializable] class Catalog { public Entry[] entries; }
        sealed class LoadRequest
        {
            public string id,file;
            public UnityWebRequest request;
            public readonly List<Action<Texture2D>> callbacks=new List<Action<Texture2D>>();
        }
        readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        readonly LinkedList<string> recent=new LinkedList<string>();
        readonly Dictionary<string,LoadRequest> pending=new Dictionary<string,LoadRequest>();
        readonly Dictionary<string,LoadRequest> active=new Dictionary<string,LoadRequest>();
        readonly Queue<LoadRequest> queue=new Queue<LoadRequest>();
        Dictionary<string,string> files;
        bool destroyed,cancelling,pumping;
        public int CachedCount=>cache.Count;
        public int ActiveLoadCount=>active.Count;
        public int PendingLoadCount=>pending.Count;
        // Profile actual reads and decodes, rather than counting subscribers.
        public int RequestsStarted { get; private set; }
        public int TexturesDecoded { get; private set; }
        public bool Contains(string id){EnsureCatalog();return id!=null&&files.ContainsKey(id);}
        void EnsureCatalog()
        {
            if(files!=null)return;
            files=new Dictionary<string,string>();
            var data=Resources.Load<TextAsset>("Data/portraits");
            var entries=data==null?null:JsonUtility.FromJson<Catalog>(data.text)?.entries;
            if(entries==null)return;
            foreach(var entry in entries)
                if(entry!=null&&!string.IsNullOrEmpty(entry.player)&&!string.IsNullOrEmpty(entry.file))files[entry.player]=entry.file;
        }
        public void Load(string id,Action<Texture2D> complete)
        {
            if(complete==null)return;
            if(destroyed||cancelling||!isActiveAndEnabled){Notify(complete,null);return;}
            EnsureCatalog();
            if(string.IsNullOrEmpty(id)){Notify(complete,null);return;}
            if(cache.TryGetValue(id,out var texture))
            {
                if(texture!=null){recent.Remove(id);recent.AddLast(id);Notify(complete,texture);return;}
                cache.Remove(id);recent.Remove(id);
            }
            if(pending.TryGetValue(id,out var existing)){existing.callbacks.Add(complete);return;}
            if(!files.TryGetValue(id,out var file)){Notify(complete,null);return;}
            var load=new LoadRequest{id=id,file=file};load.callbacks.Add(complete);
            pending.Add(id,load);queue.Enqueue(load);Pump();
        }
        void Pump()
        {
            if(pumping||destroyed||cancelling||!isActiveAndEnabled)return;
            pumping=true;
            try
            {
                while(active.Count<ConcurrentLimit&&queue.Count>0&&!destroyed&&!cancelling&&isActiveAndEnabled)
                {
                    var load=queue.Dequeue();if(!IsPending(load))continue;
                    active.Add(load.id,load);StartCoroutine(Read(load));
                }
            }
            finally{pumping=false;}
        }
        bool IsPending(LoadRequest load)=>pending.TryGetValue(load.id,out var current)&&ReferenceEquals(current,load);
        IEnumerator Read(LoadRequest load)
        {
            // Android StreamingAssets live inside the APK; provider URLs from
            // the import report are never requested by the game.
            UnityWebRequestAsyncOperation operation=null;
            try
            {
                string path=Application.streamingAssetsPath+"/Portraits/"+load.file;
                string url=path.Contains("://")?path:new Uri(path).AbsoluteUri;
                load.request=UnityWebRequestTexture.GetTexture(url,true);
                operation=load.request.SendWebRequest();RequestsStarted++;
            }
            catch(Exception){DisposeRequest(load);}
            if(operation==null){Finish(load,null);yield break;}
            yield return operation;
            if(!IsPending(load)){DisposeRequest(load);yield break;}
            Texture2D texture=null;
            try
            {
                if(load.request.result==UnityWebRequest.Result.Success)
                {
                    texture=DownloadHandlerTexture.GetContent(load.request);
                    if(texture!=null){texture.name="Portrait "+load.id;TexturesDecoded++;}
                }
            }
            catch(Exception){if(texture!=null)Destroy(texture);texture=null;}
            DisposeRequest(load);Finish(load,texture);
        }
        void Finish(LoadRequest load,Texture2D texture)
        {
            if(!IsPending(load)){if(texture!=null)Destroy(texture);return;}
            pending.Remove(load.id);active.Remove(load.id);
            if(texture!=null)
            {
                cache.Add(load.id,texture);recent.AddLast(load.id);
                while(cache.Count>CacheLimit)
                {
                    string oldest=recent.First.Value;recent.RemoveFirst();Destroy(cache[oldest]);cache.Remove(oldest);
                }
            }
            var callbacks=load.callbacks.ToArray();load.callbacks.Clear();
            foreach(var callback in callbacks)Notify(callback,destroyed||!isActiveAndEnabled?null:texture);
            Pump();
        }
        static void Notify(Action<Texture2D> callback,Texture2D texture)
        {
            // One detached UI subscriber cannot starve the other subscribers
            // or leave the next queued portrait waiting forever.
            try{callback(texture);}catch(Exception error){Debug.LogException(error);}
        }
        static void DisposeRequest(LoadRequest load)
        {
            var request=load.request;load.request=null;if(request==null)return;
            try{if(!request.isDone)request.Abort();}finally{request.Dispose();}
        }
        void CancelPending()
        {
            if(cancelling)return;
            cancelling=true;
            var loads=new List<LoadRequest>(pending.Values);
            pending.Clear();active.Clear();queue.Clear();StopAllCoroutines();
            try
            {
                foreach(var load in loads)
                {
                    DisposeRequest(load);
                    var callbacks=load.callbacks.ToArray();load.callbacks.Clear();
                    foreach(var callback in callbacks)Notify(callback,null);
                }
            }
            finally{cancelling=false;}
        }
        void OnDisable(){CancelPending();}
        void OnDestroy()
        {
            destroyed=true;CancelPending();
            foreach(var texture in cache.Values)if(texture!=null)Destroy(texture);
            cache.Clear();recent.Clear();
        }
    }
}
