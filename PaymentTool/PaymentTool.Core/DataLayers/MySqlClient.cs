using System.Data;
using System.Diagnostics;
using Dapper;
using PaymentTool.Core.DataLayers.Interfaces;
using PaymentTool.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Serilog.Context;

namespace PaymentTool.Core.DataLayers
{
    public sealed class MySqlClient: IDataBaseInterface
    {
        private readonly string _connectionString;

        private readonly ILogger<MySqlClient> _logger;

        public DataBase DataBase { get; set; }

        public MySqlConnection GetMySqlConnection()
        {
            return new MySqlConnection(_connectionString);
        }
        
        public MySqlClient(string connectionString, ILogger<MySqlClient> logger, DataBase dataBase)
        {
            _connectionString = connectionString;
            _logger = logger;
            DataBase = dataBase;
        }

        #region Query

         public IEnumerable<T> Query<T>(string name, object parameters = null, CommandType commandType = CommandType.StoredProcedure, int? commandTimeout = null)
        {
            IEnumerable<T> result;
            var sw = Stopwatch.StartNew();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                var command = new CommandDefinition(name, parameters,
                    commandTimeout : commandTimeout,
                    commandType: commandType,
                    flags: CommandFlags.NoCache);
                result = connection.Query<T>(command);
                connection.Close();
                connection.Dispose();
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

        public async Task<IEnumerable<T>> QueryAsync<T>(string name, object? parameters = null, int? commandTimeout = null)
        {
            IEnumerable<T> result;
            var sw = Stopwatch.StartNew();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                result = await connection.QueryAsync<T>(name, parameters, commandType: CommandType.StoredProcedure, commandTimeout: commandTimeout);
                await connection.CloseAsync();
                await connection.DisposeAsync();
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

        public async Task<T> QueryFirstAsync<T>(string name, object? parameters = null, int? commandTimeout = null)
        {
            T result;
            var resultCode = string.Empty;
            var sw = Stopwatch.StartNew();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                result = await connection.QueryFirstOrDefaultAsync<T>(name, parameters, commandType: CommandType.StoredProcedure, commandTimeout: commandTimeout);
                await connection.CloseAsync();
                await connection.DisposeAsync();
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

        public IEnumerable<dynamic> Query(string name, object parameters = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            IEnumerable<dynamic> result;
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                result = connection.Query(name, param: parameters, commandType: CommandType.StoredProcedure, commandTimeout: commandTimeout);
                connection.Close();
                connection.Dispose();
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
        
        public T QueryFirst<T>(string name, IDbTransaction trans , object parameters = null)
        {
            var sw = Stopwatch.StartNew();
            T result;
            try
            {
                var command = new CommandDefinition(name, parameters,
                    commandType: CommandType.StoredProcedure,
                    flags: CommandFlags.NoCache);
                result = trans.Connection.QueryFirst<T>(command);
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

        public T QueryFirst<T>(string name, object parameters = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            T result;
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                var command = new CommandDefinition(name, parameters,
                    commandTimeout: commandTimeout,
                    commandType: CommandType.StoredProcedure,
                    flags: CommandFlags.NoCache);
                result = connection.QueryFirst<T>(command);
                connection.Close();
                connection.Dispose();
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
        
        #endregion

        #region Execute

        public async Task<int> ExecuteAsync(string name, object parameters = null)
        {
            var sw = Stopwatch.StartNew();
            int result;
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                result = await connection.ExecuteAsync(name, param: parameters, commandType: CommandType.StoredProcedure);
                await connection.CloseAsync();
                await connection.DisposeAsync();
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
        
        public int Execute(string name, IDbTransaction trans, object parameters = null)
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

        public int Execute(string name, object parameters)
        {
            int result;
            var sw = Stopwatch.StartNew();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                result = connection.Execute(name, parameters, commandType: CommandType.StoredProcedure);
                connection.Close();
                connection.Dispose();
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

        #endregion
        
    }
}