using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;

namespace HR.Web.Data
{
    public class Repository<T> where T : class
    {
        protected readonly HrContext Context;
        private readonly DbSet<T> _set;

        public Repository(HrContext ctx)
        {
            Context = ctx;
            _set = ctx.Set<T>();
        }

        public IQueryable<T> GetAll(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _set;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return query;
        }

        public T Get(int id)
        {
            return _set.Find(id);
        }

        public void Add(T entity)
        {
            _set.Add(entity);
        }

        public void Update(T entity)
        {
            Context.Entry(entity).State = EntityState.Modified;
        }

        public void Remove(T entity)
        {
            _set.Remove(entity);
        }
    }
}










































