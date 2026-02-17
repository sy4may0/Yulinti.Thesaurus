using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace Yulinti.Thesaurus {
    internal class IndexServandaDto {
        [JsonPropertyName("revisio_proximus")]
        public long RevisioProximus { get; set; }
        
        [JsonPropertyName("versio")]
        public int Versio { get; set; }
        
        [JsonPropertyName("manualis")]
        public Dictionary<Guid, DataServandaDto> Manualis { get; set; } = null!;
        
        [JsonPropertyName("ordo_manualis")]
        public List<Guid> OrdoManualis { get; set; } = null!;
        
        [JsonPropertyName("automaticus")]
        public Dictionary<Guid, DataServandaDto> Automaticus { get; set; } = null!;
        
        [JsonPropertyName("ordo_automaticus")]
        public List<Guid> OrdoAutomaticus { get; set; } = null!;
        
        [JsonPropertyName("novissimus")]
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
        [JsonPropertyName("revisio")]
        public long Revisio { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [JsonPropertyName("path")]
        public string Path { get; set; } = null!;
    }

    internal class NovissimusServandaDto {
        [JsonPropertyName("methodus")]
        public string Methodus { get; set; } = null!;
        
        [JsonPropertyName("guid")]
        public Guid Guid { get; set; }
        
        [JsonPropertyName("revisio")]
        public long Revisio { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}