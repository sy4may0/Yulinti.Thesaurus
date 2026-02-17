using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface ILuditorDataServanda<T> {
        Guid LegoNovissimus();
        Task<IDataServanda<T>> Arcessere(Guid guid);
        Task Servare(T dataDTO);
        Task Servare(Guid guid, T dataDTO);
        Task Deleto(Guid guid);
        IReadOnlyList<Guid> TabulaManualis();
        IReadOnlyList<Guid> TabulaAutomaticus();
    }
}