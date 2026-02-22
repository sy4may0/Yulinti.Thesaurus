using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface ILuditorDataServanda<TNotitia, TData> {
        Task<Guid?> LegoNovissimus(CancellationToken ct = default);
        Task<IDataServanda<TData>> Arcessere(Guid guid, CancellationToken ct = default);
        Task<IDataNotitia<TNotitia>> ArcessereNotitiam(Guid guid, CancellationToken ct = default);
        Task<Guid> CreareManualis(TNotitia notitiaDTO, TData dataDTO, CancellationToken ct = default);
        Task<Guid> CreareAutomaticus(TNotitia notitiaDTO, TData dataDTO, CancellationToken ct = default);
        Task<Guid> Servare(Guid guid, TNotitia notitiaDTO, TData dataDTO, CancellationToken ct = default);
        Task Deleto(Guid guid, CancellationToken ct = default);
        Task<IReadOnlyList<Guid>> TabulaManualis(CancellationToken ct = default);
        Task<IReadOnlyList<Guid>> TabulaAutomaticus(CancellationToken ct = default);
    }
}
