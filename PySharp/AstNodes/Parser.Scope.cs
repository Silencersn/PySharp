using System.Reflection.Metadata;
using System.Xml.Linq;

namespace PySharp.AstNodes;

partial class Parser
{
    private sealed class VariableScope
    {
        private readonly IAstVariableScopeOwner? _owner;
        private readonly VariableScope? _parent;
        private readonly List<VariableScope> _children = [];
        private readonly Dictionary<string, PyVariableType> _variableTypes = [];
        private readonly List<NameNode> _nameNodeTracker = [];
        private int _isInLoop;
        private readonly HashSet<string> _closureVariables = [];

        public Dictionary<string, PyVariableType> Variables => _variableTypes;
        public bool IsRoot => _parent is null;
        public VariableScope? Parent => _parent;
        public IList<VariableScope> Children => _children;
        public IAstVariableScopeOwner? Owner => _owner;
        public bool IsCurrentFuncDef => Owner is FunctionDefNode;
        public bool IsInFuncDef => IsCurrentFuncDef || (Parent?.IsInFuncDef ?? false);
        public List<NameNode> TrackedNameNodes => _nameNodeTracker;
        public bool IsInLoop => _isInLoop > 0;
        public HashSet<string> CapturedVariables => _closureVariables;

        internal VariableScope()
        {
            _owner = null;
            _parent = null;
        }
        private VariableScope(IAstVariableScopeOwner owner, VariableScope parent)
        {
            _owner = owner;
            _parent = parent;
        }

        public VariableScope CreateScope(IAstVariableScopeOwner owner)
        {
            var scope = new VariableScope(owner, this);
            _children.Add(scope);
            return scope;
        }

        public void AddParameters(AstArgumentsNode argumentsNode)
        {
            foreach (var node in argumentsNode.PosonlyArgs)
                _variableTypes.Add(node.Arg, PyVariableType.Parameter);
            foreach (var node in argumentsNode.Args)
                _variableTypes.Add(node.Arg, PyVariableType.Parameter);
            foreach (var node in argumentsNode.KwonlyArgs)
                _variableTypes.Add(node.Arg, PyVariableType.Parameter);
            if (argumentsNode.VarArg is not null)
                _variableTypes.Add(argumentsNode.VarArg.Arg, PyVariableType.Parameter);
            if (argumentsNode.KwArg is not null)
                _variableTypes.Add(argumentsNode.KwArg.Arg, PyVariableType.Parameter);
        }

        public void TrySetLocalIfNotExistsOrUnknown(string variable)
        {
            if (!_variableTypes.TryGetValue(variable, out var type) || type is PyVariableType.Unknown)
                _variableTypes[variable] = PyVariableType.Local;
        }

        public void TryAddUnknown(string variable)
        {
            _variableTypes.TryAdd(variable, PyVariableType.Unknown);
        }

        public bool TryGetVariableType(string variable, out PyVariableType type)
        {
            return _variableTypes.TryGetValue(variable, out type);
        }

        public void SetGlobal(string variable)
        {
            _variableTypes[variable] = PyVariableType.Global;
        }

        public void SetNonlocal(string variable)
        {
            _variableTypes[variable] = PyVariableType.Nonlocal;
        }

        public void Track(NameNode nameNode)
        {
            _nameNodeTracker.Add(nameNode);
        }

        public void EnterLoop()
        {
            _isInLoop++;
        }

        public void ExitLoop()
        {
            _isInLoop--;
        }
    }

    private sealed class ScopeContext
    {
        private readonly VariableScope _root;
        private readonly Stack<VariableScope> _scopeStack;

        public VariableScope CurrentScope => _scopeStack.Peek();

        internal ScopeContext()
        {
            _root = new VariableScope();
            _scopeStack = [];
            _scopeStack.Push(_root);
        }

        public VariableScope EnterScope(IAstVariableScopeOwner owner)
        {
            var scope = CurrentScope.CreateScope(owner);
            _scopeStack.Push(scope);
            return scope;
        }

        public VariableScope ExitScope()
        {
            return _scopeStack.Pop();
        }
    }
}
