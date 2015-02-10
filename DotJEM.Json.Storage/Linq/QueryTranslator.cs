using System;
using System.Linq.Expressions;

namespace DotJEM.Json.Storage.Linq
{
    internal class QueryTranslator : ExpressionVisitor
    {
        private readonly string table;

        internal QueryTranslator(string table)
        {
            this.table = table;
        }

        internal string Translate(Expression expression)
        {
            //Console.WriteLine("Translate: " + expression);
            Visit(expression);
            return "";
        }
 
        protected override Expression VisitBlock(BlockExpression node)
        {
            Console.WriteLine("VisitBlock: " + node);
            return base.VisitBlock(node);
        }

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            Console.WriteLine("VisitCatchBlock: " + node);
            return base.VisitCatchBlock(node);
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Console.WriteLine("VisitConditional: " + node);
            return base.VisitConditional(node);
        }

        protected override Expression VisitDebugInfo(DebugInfoExpression node)
        {
            Console.WriteLine("VisitDebugInfo: " + node);
            return base.VisitDebugInfo(node);
        }

        protected override Expression VisitDefault(DefaultExpression node)
        {
            Console.WriteLine("VisitDefault: " + node);
            return base.VisitDefault(node);
        }

        protected override Expression VisitDynamic(DynamicExpression node)
        {
            Console.WriteLine("VisitDynamic: " + node);
            return base.VisitDynamic(node);
        }

        protected override ElementInit VisitElementInit(ElementInit node)
        {
            Console.WriteLine("VisitElementInit: " + node);
            return base.VisitElementInit(node);
        }

        protected override Expression VisitExtension(Expression node)
        {
            Console.WriteLine("VisitExtension: " + node);
            return base.VisitExtension(node);
        }

        protected override Expression VisitGoto(GotoExpression node)
        {
            Console.WriteLine("VisitGoto: " + node);
            return base.VisitGoto(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            Console.WriteLine("VisitIndex: " + node);
            return base.VisitIndex(node);
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            Console.WriteLine("VisitInvocation: " + node);
            return base.VisitInvocation(node);
        }

        protected override Expression VisitLabel(LabelExpression node)
        {
            Console.WriteLine("VisitLabel: " + node);
            return base.VisitLabel(node);
        }

        protected override LabelTarget VisitLabelTarget(LabelTarget node)
        {
            Console.WriteLine("VisitLabelTarget: " + node);
            return base.VisitLabelTarget(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Console.WriteLine("VisitLambda<T>: " + node);
            return base.VisitLambda(node);
        }

        protected override Expression VisitListInit(ListInitExpression node)
        {
            Console.WriteLine("VisitListInit: " + node);
            return base.VisitListInit(node);
        }

        protected override Expression VisitLoop(LoopExpression node)
        {
            Console.WriteLine("VisitLoop: " + node);
            return base.VisitLoop(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            Console.WriteLine("VisitMember: " + node);
            return base.VisitMember(node);
        }

        protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
        {
            Console.WriteLine("VisitMemberAssignment: " + node);
            return base.VisitMemberAssignment(node);
        }

        protected override MemberBinding VisitMemberBinding(MemberBinding node)
        {
            Console.WriteLine("VisitMemberBinding: " + node);
            return base.VisitMemberBinding(node);
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            Console.WriteLine("VisitMemberInit: " + node);
            return base.VisitMemberInit(node);
        }

        protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
        {
            Console.WriteLine("VisitMemberListBinding: " + node);
            return base.VisitMemberListBinding(node);
        }

        protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
        {
            Console.WriteLine("VisitMemberMemberBinding: " + node);
            return base.VisitMemberMemberBinding(node);
        }

        protected override Expression VisitNew(NewExpression node)
        {
            Console.WriteLine("VisitNew: " + node);
            return base.VisitNew(node);
        }

        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            Console.WriteLine("VisitNewArray: " + node);
            return base.VisitNewArray(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Console.WriteLine("VisitParameter: " + node);
            return base.VisitParameter(node);
        }

        protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
        {
            Console.WriteLine("VisitRuntimeVariables: " + node);
            return base.VisitRuntimeVariables(node);
        }

        protected override Expression VisitSwitch(SwitchExpression node)
        {
            Console.WriteLine("VisitSwitch: " + node);
            return base.VisitSwitch(node);
        }

        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            Console.WriteLine("VisitSwitchCase: " + node);
            return base.VisitSwitchCase(node);
        }

        protected override Expression VisitTry(TryExpression node)
        {
            Console.WriteLine("VisitTry: " + node);
            return base.VisitTry(node);
        }

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            Console.WriteLine("VisitTypeBinary: " + node);
            return base.VisitTypeBinary(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Console.WriteLine("VisitMethodCall: " + node);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            Console.WriteLine("VisitBinary: " + node);
            return base.VisitUnary(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            Console.WriteLine("VisitBinary: "+ node );
            return base.VisitBinary(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Console.WriteLine("VisitConstant: " + node);
            return base.VisitConstant(node);
        }

        protected Expression VisitMemberAccess(MemberExpression node)
        {
            Console.WriteLine("VisitMemberAccess: " + node);
            return node;
        }
    }
}