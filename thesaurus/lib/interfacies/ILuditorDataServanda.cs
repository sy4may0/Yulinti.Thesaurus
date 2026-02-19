using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface ILuditorDataServanda<T> {
        Task<Guid?> LegoNovissimus();
        Task<IDataServanda<T>> Arcessere(Guid guid);
        Task<Guid> CreareManualis(T dataDTO);
        Task<Guid> CreareAutomatics(T dataDTO);
        Task<Guid> Servare(Guid guid, T dataDTO);
        Task Deleto(Guid guid);
        Task<IReadOnlyList<Guid>> TabulaManualis();
        Task<IReadOnlyList<Guid>> TabulaAutomaticus();
    }
}