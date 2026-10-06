namespace Touchline.Core
{
    public partial class Career
    {
        public static string DomesticCupName(string country)
        {
            switch(country){case "fra":return "Coupe de France";case "eng":return "FA Cup";case "ger":return "DFB-Pokal";case "esp":return "Copa del Rey";case "ita":return "Coppa Italia";case "ned":return "KNVB Beker";case "por":return "Taça de Portugal";case "sco":return "Scottish Cup";case "bel":return "Coupe de Belgique";case "tur":return "Coupe de Turquie";case "sui":return "Coupe de Suisse";case "aut":return "Coupe d’Autriche";default:return "Coupe nationale · "+country;}
        }
    }
}
