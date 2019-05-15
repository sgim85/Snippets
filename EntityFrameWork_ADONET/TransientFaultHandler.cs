using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Net;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace MyProject.Data
{
    /// <summary>
    /// This class enables EF's connection resiliency and retry logic (supported in EF 6.0 and later)
    /// Two ways to use it: (1) Add DBConfiguration attribute to DBContext class (2) Add codeConfigurationType attribute to the entityFramework config element. 
    /// Sources: https://docs.microsoft.com/en-us/ef/ef6/fundamentals/connection-resiliency/retry-logic, https://docs.microsoft.com/en-us/ef/ef6/fundamentals/configuring/code-based, 
    /// </summary>
    public class EFDBConfiguration : DbConfiguration
    {
        public EFDBConfiguration()
        {
            SetExecutionStrategy("System.Data.SqlClient", () => new EFExecutionStrategy(3, TimeSpan.FromSeconds(2)));
        }

        class EFExecutionStrategy : DbExecutionStrategy
        {
            public EFExecutionStrategy(int retryCount, TimeSpan retryInverval) : base(retryCount, retryInverval)
            {

            }

            /// <summary>
            /// Overrides default ShouldRetryOn to be specific about what scenario warrant a re-try. Perhaps we shouldn't override and treat all exceptions the same.
            /// </summary>
            /// <param name="exception">The reported EF exception</param>
            /// <returns>True or False for whether a Retry should be carried out</returns>
            protected override bool ShouldRetryOn(Exception exception)
            {
                // -2: timeout, 53: could not open connection, 121: transport fail
                List<int> retryErrorNums = new List<int> { -2, 121, 53 };

                if (exception is SqlException)
                {
                    SqlException ex = exception as SqlException;
                    foreach (SqlError sqlError in ex.Errors)
                    {
                        if (retryErrorNums.Contains(sqlError.Number))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// This class provides transient fault handling for ADO.NET sql commands. It will retry failing commands up to 3 times in 2-second intervals.
    /// To be more specific about what errors qualify as transient, modify the IsTransient method in SqlExceptionDetectionStrategy
    /// </summary>
    public class SqlTransientFaultHandler
    {
        private RetryPolicy sqlRetryPolicy;

        public SqlTransientFaultHandler()
        {
            // Multiple strategies exist. E.g. FixedInterval, ExponentialBackoff, etc.
            // sqlRetryPolicy = new RetryPolicy<SqlExceptionDetectionStrategy>(new FixedInterval(3, TimeSpan.FromSeconds(2)));
            sqlRetryPolicy = new RetryPolicy<SqlExceptionDetectionStrategy>(new ExponentialBackoff(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));
        }

        public SqlTransientFaultHandler(int retryCount, TimeSpan retryInterval)
        {
            sqlRetryPolicy = new RetryPolicy<SqlExceptionDetectionStrategy>(new FixedInterval(retryCount, retryInterval));
        }

        public RetryPolicy SqlRetryPolicy
        {
            get
            {
                return sqlRetryPolicy;
            }
        }

        /// <summary>
        /// Executes the sql command delegate, e.g. command.ExecuteScalar, and returns the result of type T if there's no error.
        /// If there is an error it will retry execution based on the retry policy.
        /// </summary>
        /// <param name="action">The Sql command delegate to execute</param>
        public T ExecuteSqlAction<T>(Func<T> action)
        {
            return SqlRetryPolicy.ExecuteAction(() =>
            {
                return action();
            });
        }


        /// <summary>
        /// Executes a no-result sql command delegate, e.g. command.ExecuteReader.
        /// If there is an error it will retry execution based on the retry policy.
        /// </summary>
        /// <param name="action">The no-result Sql command delegate to execute</param>
        public void ExecuteSqlAction(Action action)
        {
            SqlRetryPolicy.ExecuteAction(() =>
            {
                action();
            });
        }

        class SqlExceptionDetectionStrategy : ITransientErrorDetectionStrategy
        {
            /// <summary>
            /// Defines what sorts of errors are transient
            /// </summary>
            public bool IsTransient(Exception exception)
            {
                // -2: timeout, 53: could not open connection, 121: transport fail
                List<int> retryErrorNums = new List<int> { -2, 121, 53 };

                if (exception is SqlException)
                {
                    SqlException ex = exception as SqlException;
                    foreach (SqlError sqlError in ex.Errors)
                    {
                        if (retryErrorNums.Contains(sqlError.Number))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }


    /// <summary>
    /// Factory for creating RetryPolicy types for different protocols like HTTP
    /// </summary>
    public static class RetryPolicyFactory
    {
        public static RetryPolicy CreateHttpRetryPolicy()
        {
            // More strategies exist besides ExponentialBackoff, e.g. FixedInterval
            return new RetryPolicy<HttpExecutionStrategy>(new ExponentialBackoff(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));
        }

        class HttpExecutionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex)
            {
                var transientStatusCodes = new List<HttpStatusCode>
                {
                    HttpStatusCode.GatewayTimeout,
                    HttpStatusCode.RequestTimeout,
                    HttpStatusCode.ServiceUnavailable
                };

                if (ex is WebException)
                {
                    var webEx = ex as WebException;
                    var response = webEx.Response as HttpWebResponse;
                    if (transientStatusCodes.Contains(response.StatusCode))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // TODO: Add more Create functions here, e.g. for Sql RetryPolicy
    }
}
