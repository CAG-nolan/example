using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Example.Repositories.Interfaces;

public interface IRepository<TEntity> where TEntity : class
{
    Task<int> Create(TEntity entity);
    Task<TEntity?> GetById(int id);
    Task<IEnumerable<TEntity>> GetAll();
    Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate);
    Task<TEntity?> FirstOrDefault(Expression<Func<TEntity, bool>> predicate);
    Task Update(TEntity entity);
    Task Delete(TEntity entity);
    Task DeleteById(int id);
    Task<int> Count();
    Task<int> Count(Expression<Func<TEntity, bool>> predicate);
    Task<bool> Any(Expression<Func<TEntity, bool>> predicate);
}