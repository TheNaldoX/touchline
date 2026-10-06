using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Touchline.Tests
{
    public class PortraitLoadingTests
    {
        [Serializable] class Entry { public string player,file; }
        [Serializable] class Catalog { public Entry[] entries; }
        GameObject host;

        [UnitySetUp] public IEnumerator EnterGame(){yield return new EnterPlayMode();}
        [UnityTearDown] public IEnumerator LeaveGame()
        {
            if(host!=null)UnityEngine.Object.Destroy(host);
            yield return null;
            yield return new ExitPlayMode();
        }
        PortraitStore Create(out Entry[] entries)
        {
            var text=Resources.Load<TextAsset>("Data/portraits");Assert.NotNull(text,"Bundled portrait catalogue must exist");
            entries=JsonUtility.FromJson<Catalog>(text.text).entries.Take(8).ToArray();Assert.AreEqual(8,entries.Length);
            host=new GameObject("Portrait loading integration test");return host.AddComponent<PortraitStore>();
        }
        static IEnumerator Until(Func<bool> completed)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(!completed()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(completed(),"Portrait requests failed to complete within 15 seconds");
        }
        [UnityTest] public IEnumerator ConcurrentSubscribersShareOneActualDecodeAndFailureDoesNotBlockQueuedPhotos()
        {
            var store=Create(out var entries);var shared=new List<Texture2D>();
            for(int i=0;i<4;i++)store.Load(entries[0].player,t=>shared.Add(t));
            Assert.AreEqual(1,store.RequestsStarted,"Four subscribers should open one bundled JPEG");
            yield return Until(()=>shared.Count==4);
            Assert.NotNull(shared[0]);Assert.Greater(shared[0].width,0);Assert.IsFalse(shared[0].isReadable);
            foreach(var texture in shared)Assert.AreSame(shared[0],texture,"Every subscriber must receive the same decoded texture");
            Assert.AreEqual(1,store.TexturesDecoded,"No redundant JPEG decodes for duplicate subscribers");Assert.AreEqual(1,store.CachedCount);

            // An actual unreadable-file request sits before valid JPEGs: this
            // tests the asynchronous error path, rather than a catalogue miss.
            var files=(Dictionary<string,string>)typeof(PortraitStore).GetField("files",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(store);
            files["missing-portrait-test"]="not-a-real-portrait-loading-test.jpg";
            bool failed=false;Texture2D failedTexture=shared[0];int finished=0,maxActive=0;
            store.Load("missing-portrait-test",t=>{failed=true;failedTexture=t;});
            foreach(var entry in entries.Skip(1))store.Load(entry.player,t=>{if(t!=null&&t.name=="Portrait "+entry.player)finished++;});
            Assert.AreEqual(3,store.ActiveLoadCount,"At most three files may be read concurrently");
            Assert.Greater(store.PendingLoadCount,store.ActiveLoadCount,"Remaining JPEGs should be queued");
            float deadline=Time.realtimeSinceStartup+15;
            while((!failed||finished<7)&&Time.realtimeSinceStartup<deadline)
            {
                maxActive=Math.Max(maxActive,store.ActiveLoadCount);Assert.LessOrEqual(store.ActiveLoadCount,3);yield return null;
            }
            Assert.IsTrue(failed);Assert.IsNull(failedTexture,"A failed file must complete subscribers with null");
            Assert.AreEqual(7,finished,"An unreadable JPEG must not block the following valid photos");
            Assert.AreEqual(3,maxActive);Assert.AreEqual(9,store.RequestsStarted);Assert.AreEqual(8,store.TexturesDecoded);
            Assert.AreEqual(0,store.PendingLoadCount);Assert.AreEqual(0,store.ActiveLoadCount);Assert.LessOrEqual(store.CachedCount,24);
        }
        [UnityTest] public IEnumerator DeactivationAndDestructionCompleteWaitingSubscribersAndReleaseCachedTexture()
        {
            var store=Create(out var entries);Texture2D cached=null;
            store.Load(entries[0].player,t=>cached=t);yield return Until(()=>cached!=null);
            int[] calls=new int[7];Texture2D[] results=new Texture2D[7];
            for(int i=0;i<7;i++){int index=i;store.Load(entries[i+1].player,t=>{calls[index]++;results[index]=t;});}
            Assert.AreEqual(3,store.ActiveLoadCount);Assert.AreEqual(7,store.PendingLoadCount);
            // Deactivation stops Unity coroutines: callbacks and active web
            // requests need explicit cancellation, including the queued ones.
            host.SetActive(false);
            Assert.AreEqual(0,store.ActiveLoadCount);Assert.AreEqual(0,store.PendingLoadCount);
            foreach(int count in calls)Assert.AreEqual(1,count);foreach(var result in results)Assert.IsNull(result);
            host.SetActive(true);Texture2D reused=null;store.Load(entries[0].player,t=>reused=t);
            Assert.AreSame(cached,reused,"Temporary deactivation should retain the bounded cache");
            store.Load(entries[1].player,t=>calls[0]++);
            UnityEngine.Object.Destroy(host);yield return null;yield return null;
            Assert.AreEqual(2,calls[0],"A new request completes exactly once when its owner is destroyed");
            foreach(int count in calls.Skip(1))Assert.AreEqual(1,count);
            Assert.IsTrue(cached==null,"Cached textures must be destroyed with the store");
        }
    }
}
