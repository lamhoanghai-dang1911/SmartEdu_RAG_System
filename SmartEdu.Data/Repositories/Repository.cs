using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SmartEdu.Shared.Entities;

namespace SmartEdu.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity is null) return null;
        // If the entity derives from BaseEntity and is soft-deleted, treat as not found
        if (entity is BaseEntity be && be.IsDeleted) return null;
        return entity;
    }

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> query = _dbSet;
        if (predicate != null) query = query.Where(predicate);
        return await query.ToListAsync();
    }

    public async Task AddAsync(T entity)
        => await _dbSet.AddAsync(entity);

    public void Update(T entity)
        => _dbSet.Update(entity);

    public void Delete(T entity)
        => _dbSet.Remove(entity);

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();

    public async Task<IEnumerable<T>> GetAllWithIncludeAsync(
    Expression<Func<T, bool>>? predicate = null,
    params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            query = query.Where(e => !EF.Property<bool>(e, nameof(BaseEntity.IsDeleted)));
        }

        return await query.ToListAsync();
    }
}