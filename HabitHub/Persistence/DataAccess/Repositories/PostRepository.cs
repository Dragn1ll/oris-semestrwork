using System.Collections;
using Application.Enums;
using Application.Interfaces.Repositories;
using Application.Utils;
using AutoMapper;
using Domain.Entities;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Persistence.DataAccess.Repositories;

public class PostRepository(AppDbContext context, IMapper mapper) : 
    Repository<Post, PostEntity>(context, mapper), IPostRepository
{
    public async Task<Result<IEnumerable<Post>>> GetAllByNewAsync()
    {
        try
        {
            var posts = (await _dbSet
                .AsNoTracking()
                .OrderByDescending(pe => pe.DateTime)
                .ToListAsync())
                .Select(mapper.Map<Post>);
            
            return Result<IEnumerable<Post>>.Success(posts);
        }
        catch (Exception)
        {
            return Result<IEnumerable<Post>>.Failure(new Error(ErrorType.ServerError,
                "Не удалось получить посты"));
        }
    }
}