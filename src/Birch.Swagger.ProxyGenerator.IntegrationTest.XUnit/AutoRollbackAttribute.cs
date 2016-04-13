using System;
using System.Reflection;
using System.Transactions;
using Xunit.Sdk;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest.XUnit
{
    /// <summary>
    /// Apply this attribute to your test method to automatically create a <see cref="System.Transactions.TransactionScope"/>
    /// that is rolled back when the test is finished.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoRollbackAttribute : BeforeAfterTestAttribute
    {
        TransactionScope _scope;

        public AutoRollbackAttribute()
        {
            ScopeOption = TransactionScopeOption.Required;
            IsolationLevel = IsolationLevel.ReadUncommitted;
            TimeoutInMs = -1;
        }

        /// <summary>
        /// Gets or sets the isolation level of the transaction.
        /// Default value is <see cref="IsolationLevel"/>.Unspecified.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; }

        /// <summary>
        /// Gets or sets the scope option for the transaction.
        /// Default value is <see cref="TransactionScopeOption"/>.Required.
        /// </summary>
        public TransactionScopeOption ScopeOption { get; set; }

        /// <summary>
        /// Gets or sets the timeout of the transaction, in milliseconds.
        /// By default, the transaction will not timeout.
        /// </summary>
        public long TimeoutInMs { get; set; }

        /// <summary>
        /// Rolls back the transaction.
        /// </summary>
        public override void After(MethodInfo methodUnderTest)
        {
            _scope.Dispose();
        }

        /// <summary>
        /// Creates the transaction.
        /// </summary>
        public override void Before(MethodInfo methodUnderTest)
        {
            var options = new TransactionOptions { IsolationLevel = IsolationLevel };
            if (TimeoutInMs > 0)
                options.Timeout = TimeSpan.FromMilliseconds(TimeoutInMs);

            _scope = new TransactionScope(ScopeOption, options, TransactionScopeAsyncFlowOption.Enabled);
        }
    }
}