using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Text;

namespace DevOps.Util.DotNet.Triage
{
    internal sealed class PredicateRewriter : ExpressionVisitor
    {
        internal Expression? NewExpression { get; set; }

        protected override Expression VisitParameter(ParameterExpression node) => NewExpression!;

        internal static Expression<Func<TContainer, bool>> ComposeContainerProperty<TContainer, TProperty>(
            Expression<Func<TProperty, bool>> predicate,
            string propertyName)
        {
            var parameterExpression = Expression.Parameter(typeof(TContainer), "x");
            var propertyExpression = Expression.Property(parameterExpression, propertyName);
            var rewriter = new PredicateRewriter()
            {
                NewExpression = propertyExpression
            };

            var newBody = rewriter.Visit(predicate.Body);
            return Expression.Lambda<Func<TContainer, bool>>(newBody, new[] { parameterExpression });
        }
    }
}
