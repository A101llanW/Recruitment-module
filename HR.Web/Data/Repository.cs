using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

        /// <summary>
        /// Loads an entity by primary key. With no includes, uses Find (no eager navigation load).
        /// With includes, uses a query with Include (required when lazy loading is disabled).
        /// </summary>
        public T Get(int id, params Expression<Func<T, object>>[] includes)
        {
            if (includes == null || includes.Length == 0)
            {
                return _set.Find(id);
            }

            IQueryable<T> query = _set;
            foreach (var include in includes)
            {
                if (include != null)
                {
                    query = query.Include(include);
                }
            }

            var idProperty = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProperty == null || idProperty.PropertyType != typeof(int))
            {
                throw new InvalidOperationException(
                    typeof(T).FullName + " must expose a public int Id property for Get with includes.");
            }

            var parameter = Expression.Parameter(typeof(T), "e");
            var idEquals = Expression.Equal(
                Expression.Property(parameter, idProperty),
                Expression.Constant(id, typeof(int)));
            var predicate = Expression.Lambda<Func<T, bool>>(idEquals, parameter);
            return query.FirstOrDefault(predicate);
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










































