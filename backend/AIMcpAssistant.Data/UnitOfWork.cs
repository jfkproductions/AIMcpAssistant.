using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Services;
using AIMcpAssistant.Data.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace AIMcpAssistant.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed = false;

    private IUserRepository? _users;
    private ICommandHistoryRepository? _commandHistories;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

        public IUserRepository Users { get; }
    public ICommandHistoryRepository CommandHistories { get; }

    public UnitOfWork(ApplicationDbContext context, IEncryptionService encryptionService)
    {
        _context = context;
        Users = new UserRepository(context, encryptionService);
        CommandHistories = new CommandHistoryRepository(context);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}