using System;

namespace Yulinti.Thesaurus {
    public interface IDataServanda<TData> {
        Guid Guid { get; }
        long Revisio { get; }
        DateTime Timestamp { get; }
        TData Data { get; }
    }
}