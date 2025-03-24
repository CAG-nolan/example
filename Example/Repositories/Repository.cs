using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Example.Data;
using Example.Repositories.Interfaces;

namespace Example.Repositories;
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly ApplicationDbContext _dbContext;
    protected readonly DbSet<TEntity> _dbSet;
    
    public Repository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
    }
    
    public virtual async Task<int> Create(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
        
        // Get the ID property
        var idProperty = entity.GetType().GetProperty("Id");
        if (idProperty != null)
        {
            return (int)idProperty.GetValue(entity);
        }
        
        return 0;
    }
    
    public virtual async Task<TEntity?> GetById(int id)
    {
        return await _dbSet.FindAsync(id);
    }
    
    public virtual async Task<IEnumerable<TEntity>> GetAll()
    {
        return await _dbSet.ToListAsync();
    }
    
    public virtual async Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }
    
    public virtual async Task<TEntity?> FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }
    
    public virtual async Task Update(TEntity entity)
    {
        _dbContext.Entry(entity).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }
    
    public virtual async Task Delete(TEntity entity)
    {
        _dbSet.Remove(entity);
        await _dbContext.SaveChangesAsync();
    }
    
    public virtual async Task DeleteById(int id)
    {
        var entity = await GetById(id);
        if (entity != null)
        {
            await Delete(entity);
        }
    }
    
    public virtual async Task<int> Count()
    {
        return await _dbSet.CountAsync();
    }
    
    public virtual async Task<int> Count(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }
    
    public virtual async Task<bool> Any(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }
}
