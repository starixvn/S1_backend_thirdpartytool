using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Serilog.Context;
using ThirdPartyTool.Core.DataLayers.Interfaces;
using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.DataLayers
{
    public sealed class MySqlClient : IDataBaseInterface
    {
        private readonly string _connectionString;

        private readonly ILogger<MySqlClient> _logger;

        public DataBase DataBase { get; set; }

        public MySqlClient(string connectionString, ILogger<MySqlClient> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public IEnumerable<T> Query<T>(string name, CommandType commandType = CommandType.StoredProcedure, object? parameters = null, int? commandTimeout = null)
        {
            IEnumerable<T> result;
            var sw = Stopwatch.StartNew();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                result = connection.Query<T>(name, parameters, commandType: commandType, commandTimeout: commandTimeout);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {sw.Elapsed:c}");
            return result;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string name, CommandType commandType = CommandType.StoredProcedure, DynamicParameters? parameters = null, int? commandTimeout = null)
        {
            IEnumerable<T> result;
            var sw = Stopwatch.StartNew();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                result = await connection.QueryAsync<T>(name, parameters, commandType: commandType, commandTimeout: commandTimeout);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {sw.Elapsed:c}");
            return result;
        }

        public async Task<T> QueryFirstAsync<T>(string name, CommandType commandType = CommandType.StoredProcedure, DynamicParameters? parameters = null, int? commandTimeout = null)
        {
            T result;
            var resultCode = string.Empty;
            var sw = Stopwatch.StartNew();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                result = await connection.QueryFirstOrDefaultAsync<T>(name, parameters, commandType: commandType, commandTimeout: commandTimeout);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {resultCode} {sw.Elapsed:c}");
            return result;
        }

        public async Task<int> ExecuteAsync(string name, CommandType commandType = CommandType.StoredProcedure, object? parameters = null)
        {
            var sw = Stopwatch.StartNew();
            int result;
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                result = await connection.ExecuteAsync(name, param: parameters, commandType: commandType);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {sw.Elapsed:c}");
            return result;
        }

        public IEnumerable<dynamic> Query(string name, object? parameters = null)
        {
            var sw = Stopwatch.StartNew();
            IEnumerable<dynamic> result;
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                result = connection.Query(name, param: parameters, commandType: CommandType.StoredProcedure);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {sw.Elapsed:c}");
            return result;
        }

        public int Execute(string name, IDbTransaction trans, object? parameters = null)
        {
            var sw = Stopwatch.StartNew();
            int result;
            try
            {
                result = trans.Connection.Execute(name, param: parameters, transaction: trans, commandType: CommandType.StoredProcedure);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {sw.Elapsed:c}");
            return result;
        }

        public T QueryFirst<T>(string name, IDbTransaction trans, object? parameters = null)
        {
            var sw = Stopwatch.StartNew();
            T result;
            try
            {
                result = trans.Connection.QueryFirst<T>(name, param: parameters, transaction: trans, commandType: CommandType.StoredProcedure);
            }
            catch (Exception e)
            {
                _logger.LogError(e, name);
                throw;
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug($"{name} {sw.Elapsed:c}");
            return result;
        }

        public MySqlConnection GetMySqlConnection()
        {
            return new MySqlConnection(_connectionString);
        }
    }
}
