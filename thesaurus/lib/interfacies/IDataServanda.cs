using System;

namespace Yulinti.Thesaurus {
    public interface IDataServanda<T> {
        Guid Guid { get; }
        long Revisio { get; }
        DateTime Timestamp { get; }
        T Data { get; }
    }
}