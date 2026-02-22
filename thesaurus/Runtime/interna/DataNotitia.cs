using System;

namespace Yulinti.Thesaurus {
    internal class DataNotitia<TNotitia> : IDataNotitia<TNotitia> {
        private readonly Guid _guid;
        private readonly long _revisio;
        private readonly DateTime _timestamp;
        private readonly TNotitia _notitia;

        public Guid Guid => _guid;
        public long Revisio => _revisio;
        public DateTime Timestamp => _timestamp;
        public TNotitia Notitia => _notitia;

        public DataNotitia(Guid guid, long revisio, DateTime timestamp, TNotitia notitia) {
            _guid = guid;
            _revisio = revisio;
            _timestamp = timestamp;
            _notitia = notitia;
        }
    }
}
