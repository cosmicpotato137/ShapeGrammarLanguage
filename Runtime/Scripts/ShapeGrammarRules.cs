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
            this.parent = other.parent;
        }

        public virtual void Init(SGObj parent)
        {
            this.parent = parent;
        }

        public virtual SGExp FindVar(string name)
        {
            return parent.FindVar(name);
        }

        public virtual void Produce(SGProdGen prodParent, int depth, int maxDepth, int seed)
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

        protected virtual float GetRandomFloat(float a, float b)
        {
            return parent.GetRandomFloat(a, b);
        }

        protected virtual int GetRandomInt(int a, int b)
        {
            return parent.GetRandomInt(a, b);
        }
    }

    public class SGRoot : SGObj
    {
        public Dictionary<string, SGVar> globalDefines;
        public Dictionary<string, SGVar> variables;
        public Dictionary<string, SGProducer> producers;
        public SGProducer firstProd;

        public SGProdGen sgTree;
        private Scope startScope = null;

        public System.Random rg;

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

        public void SetStartScope(Scope scope)
        {
            startScope = scope;
        }

        public override SGExp FindVar(string name)
        {
            if (variables.ContainsKey(name))
                return variables[name].value;
            throw new Exception("Variable not found: " + name);
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth = 10, int seed = -1)
        {
            if (globalDefines.ContainsKey("MAX_DEPTH"))
                maxDepth = globalDefines["MAX_DEPTH"].Get<int>();
            if (globalDefines.ContainsKey("SEED"))
                seed = globalDefines["SEED"].Get<int>();

            if (seed < 0)
                rg = new System.Random();
            else
                rg = new System.Random(seed);

            sgTree.Produce(null, depth, maxDepth, seed);
        }

        public override void Generate()
        {
            // TODO: add parent Transform to everything!
            sgTree.scope = Scope.identity.Copy();
            if (startScope != null)
                sgTree.scope = startScope;
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
            sgTree.scope = Scope.identity.Copy();
            sgTree.Init(this);
        }

        public override SGProducer GetProdReference(string name)
        {
            if (producers.ContainsKey(name))
                return producers[name];
            throw new KeyNotFoundException("No rule: " + name + " found");
        }

        protected override float GetRandomFloat(float a, float b)
        {
            return (float)(rg.NextDouble() * (double)b + (double)a);
        }

        protected override int GetRandomInt(int a, int b)
        {
            return rg.Next(a, b);
        }
    }

    public class SGExp : SGObj
    {
        public SGProdGen prodParent;

        public SGExp(string token) : base(token) { }

        public SGExp(SGExp other) : base(other)
        {
            this.prodParent = other.prodParent;
        }

        public virtual dynamic Evaluate()
        {
            throw new NotImplementedException();
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth, int seed)
        {
            this.prodParent = prodParent;
        }

        public virtual SGExp Copy()
        {
            return new SGExp(this);
        }
    }

    public class SGBool : SGExp
    {
        bool value;
        public SGBool(string token, bool b) : base(token)
        {
            value = b;
        }

        public SGBool(SGBool other) : base(other)
        {
            this.value = other.value;
        }

        public override SGExp Copy()
        {
            return new SGBool(this);
        }

        public override dynamic Evaluate()
        {
            return value;
        }
    }

    public class SGNumber : SGExp
    {
        float value;

        public SGNumber(string token, float number) : base(token)
        {
            value = number;
        }

        public SGNumber(SGNumber other) : base(other)
        {
            this.value = other.value;
        }

        public override SGExp Copy()
        {
            return new SGNumber(this);
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

        public SGString(SGString other) : base(other)
        {
            this.value = other.value;
        }

        public override SGExp Copy()
        {
            return new SGString(this);
        }

        public override dynamic Evaluate()
        {
            return value;
        }
    }

    public class SGInlineVar : SGExp
    {
        string name;
        dynamic value = null;

        public SGInlineVar(string token, string name) : base(token)
        {
            this.name = name;
        }

        public SGInlineVar(SGInlineVar other) : base(other)
        {
            this.name = other.name;
        }

        public override SGExp Copy()
        {
            return new SGInlineVar(this);
        }

        public override dynamic Evaluate()
        {
            if (value == null)
                value = prodParent.FindVar(name).Evaluate();
            return value;
        }
    }

    public class SGOpExp : SGExp
    {
        public SGExp exp1, exp2;
        public string oper;
        public dynamic value;

        public SGOpExp(SGExp e1, string op, SGExp e2) : base(op)
        {
            exp1 = e1;
            exp2 = e2;
            oper = op;
        }

        public SGOpExp(SGOpExp other) : base(other)
        {
            exp1 = other.exp1.Copy();
            exp2 = other.exp2.Copy();
            oper = other.oper;
        }

        public override SGExp Copy()
        {
            return new SGOpExp(this);
        }

        public override dynamic Evaluate()
        {
            if (value != null)
                return value;
            switch (oper)
            {
                case "==": value = exp1.Evaluate() == exp2.Evaluate(); break;
                case "!=": value = exp1.Evaluate() != exp2.Evaluate(); break;
                case "<=": value = exp1.Evaluate() <= exp2.Evaluate(); break;
                case ">=": value = exp1.Evaluate() >= exp2.Evaluate(); break;
                case ">":  value = exp1.Evaluate() > exp2.Evaluate(); break;
                case "<":  value = exp1.Evaluate() < exp2.Evaluate(); break;
                case "+":  value = exp1.Evaluate() + exp2.Evaluate(); break;
                case "-":  value = exp1.Evaluate() - exp2.Evaluate(); break;
                case "*":  value = exp1.Evaluate() * exp2.Evaluate(); break;
                case "/":  value = exp1.Evaluate() / exp2.Evaluate(); break;
                case "**": value = Math.Pow(exp1.Evaluate(), exp2.Evaluate()); break;
                default: throw new InvalidOperationException("Operator '" + oper + "' not recognized");
            }
            return value;
        }

        public override void Init(SGObj parent)
        {
            base.Init(parent);
            exp1.Init(this);
            exp2.Init(this);
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth, int seed)
        {
            this.prodParent = prodParent;
            exp1.Produce(prodParent, depth, maxDepth, seed);
            exp2.Produce(prodParent, depth, maxDepth, seed);
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
        public Dictionary<string, int> arguments;
        public SGExp condition;

        // non-normalized probability that a rule gets chosen
        public List<SGExp> probs;

        public SGProducer(string token, LinkedList<SGGeneratorBase> ruleList, List<string> args = null, SGExp cond = null) : base(token)
        {
            this.ruleLists = new List<LinkedList<SGGeneratorBase>> { ruleList };
            probs = new List<SGExp>();

            arguments = new Dictionary<string, int>();
            if (args != null && args.Count > 0)
                for (int i = 0; i < args.Count; i++)
                    arguments.Add(args[i], i);
            if (cond == null)
                cond = new SGBool(null, true);
            condition = cond;
        }
        
        public SGProducer(string token, List<LinkedList<SGGeneratorBase>> ruleLists, List<SGExp> pList, List<string> args = null, SGExp cond = null) : base(token)
        {
            this.ruleLists = ruleLists;
            probs = pList;

            arguments = new Dictionary<string, int>();
            if (args != null && args.Count > 0)
                for (int i = 0; i < args.Count; i++)
                    arguments.Add(args[i], i);
            if (cond == null)
                cond = new SGBool(null, true);
            condition = cond;
        }
        
        public SGProducer(SGProducer other) : base(other)
        {
            this.ruleLists = other.ruleLists;
            this.probs = other.probs;
            this.condition = other.condition;
        }

        public List<SGGeneratorBase> GetChildren()
        {
            // get the index of the set of rules to call
            int idx = 0;
            if (probs.Count > 0)
            {
                // normalize probabilities
                // todo: maybe optimize this?
                List<float> probs = new List<float>();
                float mag = 0;
                foreach (SGExp f in this.probs)
                    mag += f.Evaluate();
                float tot = 0;
                for (int i = 0; i < this.probs.Count; i++)
                {
                    probs.Add(tot + this.probs[i].Evaluate() / mag);
                    tot += this.probs[i].Evaluate() / mag;
                }

                // get index based on a random number
                float rand = GetRandomFloat(0.0f, 1.0f); // todo: optimize?
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
            condition.Init(this);
            foreach (var lst in ruleLists)
                foreach (SGGeneratorBase gen in lst)
                    gen.Init(this);
            foreach (var exp in probs)
                exp.Init(this);
        }

        // returns -1 for no argument; otherwise returns the index
        // of the argument in the parameter list
        public int ContainsArgument(string name)
        {
            if (arguments.ContainsKey(name))
                return arguments[name];
            return -1;
        }
    }

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
            this.parent = other.parent;
            this.depth = other.depth;
            this.parameters = new List<SGExp>();
            foreach (SGExp param in other.parameters)
                this.parameters.Add(param.Copy());
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

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth, int seed)
        {
            this.prodParent = prodParent;
            foreach (var p in parameters)
                p.Produce(this.prodParent, depth, maxDepth, seed);
        }

        public override void Generate()
        {
            throw new NotImplementedException();
        }

        public override SGExp FindVar(string name)
        {
            return prodParent.FindVar(name);
        }
    }

    // inline call to a production rule
    public class SGProdGen : SGGeneratorBase
    {
        // set producer to depth first or breadth first
        public bool depthFirst { get; private set; }
        // start with the parent's scope?
        public bool adoptParentScope;

        public SGProducer prodReference = null;
        public SGExp condition = null;
        public List<SGExp> probs;
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
            // TODO recursive copy on children for production tree
            if (scope != null)
                this.scope = other.scope.Copy();
            this.scopeStack = other.scopeStack;
            this.gameObjects = other.gameObjects;
            this.depthFirst = other.depthFirst;
            this.adoptParentScope = other.adoptParentScope;
        }

        public override SGGeneratorBase Copy()
        {
            return new SGProdGen(this);
        }

        public override void Produce(SGProdGen prodParent, int depth, int maxDepth, int seed)
        {
            if (parameters.Count != GetProdReference(token).arguments.Count)
                throw new MissingMethodException("Incorrect number of arguments in rule: " + token);
            // end if max depth has been reached
            if (depth > maxDepth)
                return;

            base.Produce(prodParent, depth, maxDepth, seed);
            this.prodParent = prodParent;

            // copy condition from producer
            condition = GetProdReference(token).condition.Copy();
            condition.Produce(this, depth, maxDepth, seed);
            if (condition.Evaluate() == false)
                return;

            // copy probabilities from producer
            probs = new List<SGExp>();
            foreach (var p in GetProdReference(token).probs)
            {
                SGExp newp = p.Copy();
                newp.Produce(this, depth, maxDepth, seed);
                probs.Add(newp);
            }

            // get the index of the set of rules to call
            int idx = 0;
            if (probs.Count > 0)
            {
                // normalize probabilities
                // todo: maybe optimize this?
                var p = new List<float>();
                float mag = 0;
                foreach (SGExp f in probs)
                    mag += f.Evaluate();
                float tot = 0;
                for (int i = 0; i < probs.Count; i++)
                {
                    p.Add(tot + probs[i].Evaluate() / mag);
                    tot += probs[i].Evaluate() / mag;
                }

                // get index based on a random number
                float rand = GetRandomFloat(0.0f, 1.0f); // todo: optimize?
                while (p[idx] < rand)
                    idx++;
                if (idx == probs.Count)
                    idx--;
            }

            // evaluate depth first
            // todo: add add multiple queues for handling priority
            generatorRules = new List<SGGeneratorBase>();
            foreach (SGGeneratorBase rule in GetProdReference(token).ruleLists[idx])
            {
                var r = rule.Copy();
                r.Produce(this, depth + 1, maxDepth, seed);
                generatorRules.Add(r);
            }


            //generatorRules = GetProdReference(token).GetChildren();

            //foreach (SGGeneratorBase gen in generatorRules)
            //    gen.Produce(this, depth + 1, maxDepth, seed);
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
            if (generatorRules == null)
                return;

            // get parent and update scope
            if (prodParent != null && adoptParentScope)
            {
                this.scope = prodParent.scope.Copy();
                this.gameObjects = prodParent.gameObjects;
            }

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
            return new SGGenerator<T1, T2, T3>(this);
        }

        public override void Generate()
        {
            callback(prodParent, (T1)parameters[0].Evaluate(), (T2)parameters[1].Evaluate(), (T3)parameters[2].Evaluate());
        }
    }
}