using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface ILuditorDataServanda<T> {
        Guid LegoNovissimus();
        Task<IDataServanda<T>> Arcessere(Guid guid);
        Task<Guid> Servare(T dataDTO);
        Task<Guid> Servare(Guid guid, T dataDTO);
        Task<Guid> ServareAutomaticus(T dataDTO);
        Task Deleto(Guid guid);
        IReadOnlyList<Guid> TabulaManualis();
        IReadOnlyList<Guid> TabulaAutomaticus();
    }
}