using System;

namespace Yulinti.Thesaurus {
    internal class DataServanda<T> : IDataServanda<T> {
        private readonly Guid _guid;
        private readonly long _revisio;
        private readonly DateTime _timestamp;
        private readonly T _data;

        public Guid Guid => _guid;
        public long Revisio => _revisio;
        public DateTime Timestamp => _timestamp;
        public T Data => _data;

        public DataServanda(Guid guid, long revisio, DateTime timestamp, T data) {
            _guid = guid;
            _revisio = revisio;
            _timestamp = timestamp;
            _data = data;
        }
    }
}