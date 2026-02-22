using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace Yulinti.Thesaurus {
    internal class IndexServandaDto {
        [JsonProperty("revisio_proximus")]
        public long RevisioProximus { get; set; }
        
        [JsonProperty("versio")]
        public int Versio { get; set; }
        
        [JsonProperty("manualis")]
        public Dictionary<Guid, DataServandaDto> Manualis { get; set; } = null!;
        
        [JsonProperty("ordo_manualis")]
        public List<Guid> OrdoManualis { get; set; } = null!;
        
        [JsonProperty("automaticus")]
        public Dictionary<Guid, DataServandaDto> Automaticus { get; set; } = null!;
        
        [JsonProperty("ordo_automaticus")]
        public List<Guid> OrdoAutomaticus { get; set; } = null!;
        
        [JsonProperty("novissimus")]
        public NovissimusServandaDto? Novissimus { get; set; }

        public IndexServandaDto() {
            RevisioProximus = 0;
            Versio = 0;
            Manualis = new Dictionary<Guid, DataServandaDto>();
            OrdoManualis = new List<Guid>();
            Automaticus = new Dictionary<Guid, DataServandaDto>();
            OrdoAutomaticus = new List<Guid>();
            Novissimus = null;
        }
    }

    internal class DataServandaDto {
        [JsonProperty("revisio")]
        public long Revisio { get; set; }
        
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; } = null!;

        [JsonProperty("path_notitia")]
        public string PathNotitia { get; set; } = null!;
    }

    internal class NovissimusServandaDto {
        [JsonProperty("methodus")]
        public string Methodus { get; set; } = null!;
        
        [JsonProperty("guid")]
        public Guid Guid { get; set; }
        
        [JsonProperty("revisio")]
        public long Revisio { get; set; }
        
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
