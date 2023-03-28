using System;
using System.Collections.Generic;
using UnityEngine;

namespace cosmicpotato.sgl
{
    // Grammar rule classes

    // base class for all shape grammar classes
    public class SGObj
    {
        public string token;
        public SGObj parent;

        protected SGObj(string token)
        {
            this.token = token;
        }

        protected SGObj(SGObj other)
        {
            this.token = other.token;
        }

        public virtual void Init(SGObj parent)
        {
            this.parent = parent;
            return;
        }

        public virtual SGExp FindVar(string name)
        {
            return parent.FindVar(name);
        }

        public virtual void Produce(SGProdGen prodParent, int depth, int maxDepth, int maxOper, int seed)
        {
            return;
        }

        public virtual void Generate()
        {
            return;
        }

        public virtual SGProducer GetProdReference(string name)
        {
            return parent.GetProdReference(name);
        }
    }

    public class SGRoot : SGObj
    {
        public Dictionary<string, SGVar> globalDefines;
        public Dictionary<string, SGVar> variables;
        public Dictionary<string, SGProducer> producers;
        public SGProducer firstProd;

        public SGProdGen sgTree;

        public SGRoot(List<SGProducer> prods) : base("ROOT")
        {
            firstProd = prods[0];
            producers = new Dictionary<string, SGProducer>();
            foreach (var prod in prods)
                producers.Add(prod.token, prod);
            parent = null;
            globalDefines = new Dictionary<string, SGVar>();
        }

        public SGRoot(List<SGProducer> prods, Dictionary<string, SGVar> defs) : base("ROOT")
        {
            firstProd = prods[0];
            producers = new Dictionary<string, SGProducer>();
            foreach (var prod in prods)
                producers.Add(prod.token, prod);
            parent = null;
            globalDefines = defs;
        }

        public override SGExp FindVar(string name)
        {
            if (variables.ContainsKey(name))
                return variables[name].value;
            throw new Exception("Variable not found: " + name);
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth = 10, int maxOper = 10000, int seed = 12345)
        {
            if (globalDefines.ContainsKey("MAX_DEPTH"))
                depth = globalDefines["MAX_DEPTH"].Get<int>();
            if (globalDefines.ContainsKey("MAX_OPER"))
                maxOper = globalDefines["MAX_OPER"].Get<int>();
            if (globalDefines.ContainsKey("SEED"))
                seed = globalDefines["SEED"].Get<int>();

            sgTree.Produce(null, depth, maxDepth, maxOper, seed);
        }

        public override void Generate()
        {
            sgTree.Generate();
        }

        public override void Init(SGObj parent)
        {
            if (firstProd.arguments.Count > 0)
                throw new ArgumentException("The first rule: " + firstProd.token + " must not have arguments!");

            this.parent = parent;
            foreach (var prod in producers)
                prod.Value.Init(this);
            foreach (var exp in globalDefines)
                exp.Value.Init(this);
            foreach (var v in variables)
                v.Value.Init(this);


            sgTree = new SGProdGen("BEGIN", new List<SGExp>());
            sgTree.prodReference = firstProd;
            sgTree.scope = Scope.identity;
        }

        public override SGProducer GetProdReference(string name)
        {
            if (producers.ContainsKey(name))
                return producers[name];
            throw new KeyNotFoundException("No rule: " + name + " found");
        }
    }

    public class SGExp : SGObj
    {
        public SGExp(string token) : base(token) { }

        public virtual dynamic Evaluate()
        {
            throw new NotImplementedException();
        }
    }

    public class SGNumber : SGExp
    {
        float value;

        public SGNumber(string token, float number) : base(token)
        {
            value = number;
        }

        public override dynamic Evaluate()
        {
            return value;
        }
    }

    public class SGString : SGExp
    {
        string value;
        public SGString(string token, string str) : base(token)
        {
            value = str;
        }

        public override dynamic Evaluate()
        {
            return value;
        }
    }

    public class SGInlineVar : SGExp
    {
        string name;
        public SGInlineVar(string token, string name) : base(token)
        {
            this.name = name;
        }

        public override dynamic Evaluate()
        {
            return FindVar(name).Evaluate();
        }
    }

    // holds shape grammar variable
    public class SGVar : SGObj
    {
        public SGExp value;

        public SGVar(string token, SGExp value) : base(token)
        {
            this.value = value;
        }

        public SGVar(SGVar other) : base(other)
        {
            this.value = other.value;
        }

        public T Get<T>()
        {
            return (T)value.Evaluate();
        }

        public void Set(dynamic value)
        {
            this.value = value;
        }
    }

    // holds definition for a shape grammar production rule
    public class SGProducer : SGObj
    {
        // list of one or more sets of rules to follow
        public List<LinkedList<SGGeneratorBase>> ruleLists;
        public Dictionary<SGExp, int> arguments;

        // non-normalized probability that a rule gets chosen
        public List<SGExp> p;

        // reference to the external queue of operations
        public static LinkedList<SGGeneratorBase> opQueue;
        // random number generator
        public static System.Random rg = new System.Random(1234);


        public SGProducer(string token, LinkedList<SGGeneratorBase> ruleList, List<SGExp> args = null) : base(token)
        {
            this.ruleLists = new List<LinkedList<SGGeneratorBase>> { ruleList };
            p = new List<SGExp>();

            arguments = new Dictionary<SGExp, int>();
            if (args != null && args.Count > 0)
                for (int i = 0; i < args.Count; i++)
                    arguments.Add(args[i], i);
        }
        
        public SGProducer(string token, List<LinkedList<SGGeneratorBase>> ruleLists, List<SGExp> pList, List<SGExp> args = null) : base(token)
        {
            this.ruleLists = ruleLists;
            p = pList;

            arguments = new Dictionary<SGExp, int>();
            if (args != null && args.Count > 0)
                for (int i = 0; i < args.Count; i++)
                    arguments.Add(args[i], i);
        }
        
        public SGProducer(SGProducer other) : base(other)
        {
            this.ruleLists = other.ruleLists;
            this.p = other.p;
        }

        public List<SGGeneratorBase> GetChildren()
        {
            // get the index of the set of rules to call
            int idx = 0;
            if (p.Count > 0)
            {
                // normalize probabilities
                // todo: maybe optimize this?
                List<float> probs = new List<float>();
                float mag = 0;
                foreach (SGExp f in p)
                    mag += f.Evaluate();
                float tot = 0;
                for (int i = 0; i < p.Count; i++)
                {
                    probs.Add(tot + p[i].Evaluate() / mag);
                    tot += p[i].Evaluate() / mag;
                }

                // get index based on a random number
                float rand = (float)rg.NextDouble();
                while (probs[idx] < rand)
                    idx++;
                if (idx == probs.Count)
                    idx--;
            }

            // get the set of rules
            var rules = ruleLists[idx];

            // evaluate depth first
            // todo: add add multiple queues for handling priority
            List<SGGeneratorBase> newList = new List<SGGeneratorBase>();
            foreach (SGGeneratorBase rule in rules)
                newList.Add(rule.Copy());

            return newList;
        }

        public override void Init(SGObj parent)
        {
            this.parent = parent;
            foreach (var lst in ruleLists)
                foreach (SGGeneratorBase gen in lst)
                    gen.Init(this);
            foreach (var exp in p)
                exp.Init(this);
            foreach (var arg in arguments)
                arg.Key.Init(this);
        }

        // returns -1 for no argument; otherwise returns the index
        // of the argument in the parameter list
        public int ContainsArgument(string name)
        {
            foreach (SGExp arg in arguments.Keys)
                if ((string)arg.Evaluate() == name)
                    return arguments[arg];
            return -1;
        }
    }

    // base class for all 'called' items in production rules
    //public class SGG : SGObj
    //{


    //    protected SGRule(string token) : base(token)
    //    {
    //    }

    //    protected SGRule(SGRule other) : base(other)
    //    {
    //    }
    //}


    // all rules that don't directly reference producers
    // used for placing geometry and manipulating scopes
    // subclasses for different numbers of parameters
    public class SGGeneratorBase : SGObj
    {
        public int depth;
        public SGProdGen prodParent;

        // parameters for the callback to be used at runtime
        public List<SGExp> parameters;

        public SGGeneratorBase(string token) : base(token)
        {
        }

        public SGGeneratorBase(SGGeneratorBase other) : base(other)
        {
            this.parameters = other.parameters;
            this.parent = other.parent;
        }

        public override void Init(SGObj parent)
        {
            this.parent = parent;
            foreach (SGExp exp in parameters)
                exp.Init(this);
        }

        public virtual SGGeneratorBase Copy()
        {
            throw new NotImplementedException();
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth, int maxOper, int seed)
        {
            this.prodParent = prodParent;
        }

        public override void Generate()
        {
            throw new NotImplementedException();
        }
    }

    // inline call to a production rule
    public class SGProdGen : SGGeneratorBase
    {
        // function find the producer at runtime
        //public Func<SGProducer> callback;

        // set producer to depth first or breadth first
        public bool depthFirst { get; private set; }
        // start with the parent's scope?
        public bool adoptParentScope;

        public SGProducer prodReference = null;
        public Scope scope;                     // current scope
        private LinkedList<Scope> scopeStack;   // scope history
        public List<GameObject> gameObjects;
        public List<SGGeneratorBase> generatorRules;

        public SGProdGen(string token, List<SGExp> parameters, bool adoptParentScope = true, bool depthFirst = false) : base(token)
        {
            this.parameters = parameters;
            scopeStack = new LinkedList<Scope>();
            //this.callback = callback;
            this.depthFirst = depthFirst;
            this.adoptParentScope = adoptParentScope;
            this.gameObjects = new List<GameObject>();
        }

        public SGProdGen(SGProdGen other) : base(other)
        {
            //this.callback = other.callback;
            this.scope = other.scope.Copy();
            this.scopeStack = other.scopeStack;
            this.gameObjects = other.gameObjects;
            this.depthFirst = other.depthFirst;
            this.depth = other.depth;
            this.parameters = other.parameters;
        }

        public override SGGeneratorBase Copy()
        {
            // TODO recursive copy on children for production tree
            var pg = new SGProdGen(this.token, this.parameters);
            if (scope != null)
                pg.scope = scope.Copy();
            pg.scopeStack = scopeStack;
            pg.gameObjects = gameObjects;
            pg.parent = parent;
            pg.depth = depth;
            pg.depthFirst = depthFirst;
            pg.adoptParentScope = adoptParentScope;
            return pg;
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth, int maxOper, int seed)
        {
            if (parameters.Count != GetProdReference(token).arguments.Count)
                throw new MissingMethodException("Incorrect number of arguments in rule: " + token);

            this.prodParent = prodParent;
            // end if max depth has been reached
            if (depth > maxDepth)
                return;

            // get parent and update scope
            if (parent != null && adoptParentScope)
            {
                this.scope = prodParent.scope.Copy();
                this.gameObjects = prodParent.gameObjects;
            }

            generatorRules = GetProdReference(token).GetChildren();

            foreach (SGGeneratorBase gen in generatorRules)
                gen.Produce(this, depth + 1, maxDepth, maxOper, seed);
        }

        public override SGProducer GetProdReference(string name)
        {
            if (prodReference == null)
                prodReference = parent.GetProdReference(name);
            return prodReference;
        }

        public override SGExp FindVar(string name)
        {
            var i = GetProdReference(token).ContainsArgument(name);
            if (i >= 0)
                return parameters[i];
            return parent.FindVar(name);
        }

        public override void Generate()
        {
            if (generatorRules != null)
            foreach (SGGeneratorBase gen in generatorRules)
                gen.Generate();
        }

        // save and load transforms to and from the stack
        public void SaveTransform()
        {
            scopeStack.AddLast(scope.Copy());
        }

        public void LoadTransform()
        {
            if (scopeStack.Last == null)
            {
                Debug.LogWarning("Trying to pop from empty stack");
                return;
            }
            scope = scopeStack.Last.Value.Copy();
            scopeStack.RemoveLast();
        }

    }

    public class SGGenerator : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen> callback;

        public SGGenerator(string token, Action<SGProdGen> callback) : base(token)
        {
            this.callback = callback;
            parameters = new List<SGExp>();
        }

        public SGGenerator(SGGenerator other) : base(other)
        {
            this.callback = other.callback;
        }
        public override SGGeneratorBase Copy()
        {
            var n = new SGGenerator(token, callback);
            n.parent = parent;
            n.depth = depth;
            n.parameters = parameters;

            return n;
        }

        public override void Generate()
        {
            callback(prodParent);
        }
    }

    public class SGGenerator<T1> : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen, T1> callback;

        public SGGenerator(string token, Action<SGProdGen, T1> callback) : base(token)
        {
            this.callback = callback;
            parameters = new List<SGExp>();
        }

        public SGGenerator(SGGenerator<T1> other) : base(other)
        {
            this.callback = other.callback;
        }

        public override SGGeneratorBase Copy()
        {
            var n = new SGGenerator<T1>(token, callback);
            n.parent = parent;
            n.depth = depth;
            n.parameters = parameters;

            return n;
        }

        public override void Generate()
        {
            callback(prodParent, (T1)parameters[0].Evaluate());
        }
    }

    public class SGGenerator<T1, T2> : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen, T1, T2> callback;

        public SGGenerator(string token, Action<SGProdGen, T1, T2> callback) : base(token)
        {
            this.callback = callback;
            parameters = new List<SGExp>();
        }

        public SGGenerator(SGGenerator<T1, T2> other) : base(other)
        {
            this.callback = other.callback;
        }

        public override SGGeneratorBase Copy()
        {
            var n = new SGGenerator<T1, T2>(token, callback);
            n.parent = parent;
            n.depth = depth;
            n.parameters = parameters;

            return n;
        }

        public override void Generate()
        {
            callback(prodParent, (T1)parameters[0].Evaluate(), (T2)parameters[1].Evaluate());
        }
    }

    public class SGGenerator<T1, T2, T3> : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen, T1, T2, T3> callback;

        public SGGenerator(string token, Action<SGProdGen, T1, T2, T3> callback) : base(token)
        {
            this.callback = callback;
            parameters = new List<SGExp>();
        }

        public SGGenerator(SGGenerator<T1, T2, T3> other) : base(other)
        {
            this.callback = other.callback;
        }

        public override SGGeneratorBase Copy()
        {
            var n = new SGGenerator<T1, T2, T3>(token, callback);
            n.parent = parent;
            n.depth = depth;
            n.parameters = parameters;
            return n;
        }

        public override void Generate()
        {
            callback(prodParent, (T1)parameters[0].Evaluate(), (T2)parameters[1].Evaluate(), (T3)parameters[2].Evaluate());
        }
    }
}