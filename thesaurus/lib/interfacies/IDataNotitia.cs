using System;

namespace Yulinti.Thesaurus {
    public interface IDataNotitia<TNotitia> {
        Guid Guid { get; }
        long Revisio { get; }
        DateTime Timestamp { get; }
        TNotitia Notitia { get; }
    }
}
