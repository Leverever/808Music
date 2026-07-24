using _808Music.Application.Common.Search;
using _808Music.Domain.Static;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace _808Music.Infrastructure.Persistence.Repositories
{
    public sealed class GenreRepository
        : EfRepository<Genre, int, GenreSearchObject>
    {
        public GenreRepository(DbContext dbContext)
            : base(dbContext)
        {
        }

        protected override IQueryable<Genre> ApplyFiltering(
            IQueryable<Genre> query,
            GenreSearchObject search)
        {
            if (!string.IsNullOrWhiteSpace(search.Name))
            {
                query = query.Where(x => x.Name.Contains(search.Name));
            }

            if (!string.IsNullOrWhiteSpace(search.Tag))
            {
                query = query.Where(x => x.Tag.Contains(search.Tag));
            }

            return query;
        }

        protected override IQueryable<Genre> ApplySorting(
            IQueryable<Genre> query,
            GenreSearchObject search)
        {
            return query.OrderBy(x => x.Name);
        }
    }
}
