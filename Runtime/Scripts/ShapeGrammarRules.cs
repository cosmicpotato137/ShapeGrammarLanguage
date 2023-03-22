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

        protected SGObj(string token)
        {
            this.token = token;
        }

        protected SGObj(SGObj other)
        {
            this.token = other.token;
        }
    }

    // holds shape grammar variable
    public class SGVar : SGObj
    {
        private dynamic value;

        public SGVar(string token, dynamic value) : base(token)
        {
            this.value = value;
        }

        public SGVar(SGVar other) : base(other)
        {
            this.value = other.value;
        }

        public T Get<T>()
        {
            return (T)value;
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
        public List<LinkedList<SGRule>> ruleLists;

        // non-normalized probability that a rule gets chosen
        public List<float> p;

        // reference to the external queue of operations
        public static LinkedList<SGRule> opQueue;
        // random number generator
        public static System.Random rg = new System.Random(1234);


        public SGProducer(string token) : base(token)
        {
            ruleLists = new List<LinkedList<SGRule>>();
            p = new List<float>();
        }

        public SGProducer(SGProducer other) : base(other)
        {
            this.ruleLists = other.ruleLists;
            this.p = other.p;
        }

        public void PushChildren(SGProdGen prodGen)
        {
            // get the index of the set of rules to call
            int idx = 0;
            if (p.Count > 0)
            {
                // normalize probabilities
                // todo: maybe optimize this?
                List<float> ps = new List<float>();
                float mag = 0;
                foreach (float f in p)
                    mag += f;
                float tot = 0;
                for (int i = 0; i < p.Count; i++)
                {
                    ps.Add(tot + p[i] / mag);
                    tot += p[i] / mag;
                }

                // get index based on a random number
                float rand = (float)rg.NextDouble();
                while (ps[idx] < rand)
                    idx++;
                if (idx == ps.Count)
                    idx--;
            }

            // get the set of rules
            var rules = ruleLists[idx];

            // evaluate depth first
            // todo: add add multiple queues for handling priority
            if (prodGen.depthFirst)
            {
                LinkedListNode<SGRule> n = rules.Last;
                while (n != null)
                {
                    var rule = n.Value;
                    rule.depth = prodGen.depth + 1;
                    rule.parent = prodGen.parent;
                    opQueue.AddAfter(opQueue.First, rule.Copy());
                    n = n.Previous;
                }
            }
            // evaluate breadth first
            else
            {
                foreach (SGRule rule in rules)
                {
                    rule.depth = prodGen.depth + 1;
                    rule.parent = prodGen;
                    opQueue.AddLast(rule.Copy());
                }
            }
        }
    }

    // base class for all 'called' items in production rules
    public class SGRule : SGObj
    {
        public SGProdGen parent;
        public static int maxDepth;
        public int depth;


        protected SGRule(string token) : base(token)
        {
        }

        protected SGRule(SGRule other) : base(other)
        {
            this.parent = other.parent;
        }

        public virtual SGRule Copy()
        {
            throw new NotImplementedException();
        }

        public virtual void Call()
        {
            throw new NotImplementedException();
        }
    }

    // inline call to a production rule
    public class SGProdGen : SGRule
    {
        // function find the producer at runtime
        public Func<SGProducer> callback;
        // producer to call
        private SGProducer prod;

        // set producer to depth first or breadth first
        public bool depthFirst { get; private set; }
        // start with the parent's scope?
        public bool adoptParentScope;

        public Scope scope;                     // current scope
        private LinkedList<Scope> scopeStack;   // scope history
        public List<GameObject> gameObjects;        // instantiated GameObjects

        public SGProdGen(string token, Func<SGProducer> callback, bool adoptParentScope = true, bool depthFirst = false) : base(token)
        {
            scopeStack = new LinkedList<Scope>();
            this.callback = callback;
            this.depthFirst = depthFirst;
            this.adoptParentScope = adoptParentScope;
            this.gameObjects = new List<GameObject>();
        }

        public SGProdGen(SGProdGen other) : base(other)
        {
            this.callback = other.callback;
            this.scope = other.scope.Copy();
            this.scopeStack = other.scopeStack;
            this.gameObjects = other.gameObjects;
            this.depthFirst = other.depthFirst;
            this.depth = other.depth;
        }

        public override SGRule Copy()
        {
            var pg = new SGProdGen(this.token, this.callback);
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

        public override void Call()
        {
            // end if max depth has been reached
            if (depth > maxDepth)
                return;

            // find producer if not memoized
            if (prod == null)
                prod = callback();

            // get parent and update scope
            if (parent != null && adoptParentScope)
            {
                this.scope = parent.scope.Copy();
                this.gameObjects = parent.gameObjects;
            }

            // pass scope and gameobject down the tree
            prod.PushChildren(this);
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

    // all rules that don't directly reference producers
    // used for placing geometry and manipulating scopes
    // subclasses for different numbers of parameters
    public class SGGeneratorBase : SGRule
    {
        // parameters for the callback to be used at runtime
        public dynamic[] parameters;

        public SGGeneratorBase(string token) : base(token)
        {
        }

        public SGGeneratorBase(SGGeneratorBase other) : base(other)
        {
            this.parameters = other.parameters;
        }
    }

    public class SGGenerator : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen> callback;

        public SGGenerator(string token, Action<SGProdGen> callback) : base(token)
        {
            this.callback = callback;
            parameters = new dynamic[0];
        }

        public SGGenerator(SGGenerator other) : base(other)
        {
            this.callback = other.callback;
        }
        public override SGRule Copy()
        {
            var n = new SGGenerator(token, callback);
            n.parent = parent;
            n.depth = depth;
            parameters.CopyTo(n.parameters, 0);

            return n;
        }

        public override void Call()
        {
            callback(parent);
        }
    }

    public class SGGenerator<T1> : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen, T1> callback;

        public SGGenerator(string token, Action<SGProdGen, T1> callback) : base(token)
        {
            this.callback = callback;
            parameters = new dynamic[1];
        }

        public SGGenerator(SGGenerator<T1> other) : base(other)
        {
            this.callback = other.callback;
        }

        public override SGRule Copy()
        {
            var n = new SGGenerator<T1>(token, callback);
            n.parent = parent;
            n.depth = depth;
            parameters.CopyTo(n.parameters, 0);

            return n;
        }

        public override void Call()
        {
            callback(parent, (T1)parameters[0]);
        }
    }

    public class SGGenerator<T1, T2> : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen, T1, T2> callback;

        public SGGenerator(string token, Action<SGProdGen, T1, T2> callback) : base(token)
        {
            this.callback = callback;
            parameters = new dynamic[2];
        }

        public SGGenerator(SGGenerator<T1, T2> other) : base(other)
        {
            this.callback = other.callback;
        }

        public override SGRule Copy()
        {
            var n = new SGGenerator<T1, T2>(token, callback);
            n.parent = parent;
            n.depth = depth;
            parameters.CopyTo(n.parameters, 0);

            return n;
        }

        public override void Call()
        {
            callback(parent, (T1)parameters[0], (T2)parameters[1]);
        }
    }

    public class SGGenerator<T1, T2, T3> : SGGeneratorBase
    {
        // callback to be used at runtime
        private Action<SGProdGen, T1, T2, T3> callback;

        public SGGenerator(string token, Action<SGProdGen, T1, T2, T3> callback) : base(token)
        {
            this.callback = callback;
            parameters = new dynamic[3];
        }

        public SGGenerator(SGGenerator<T1, T2, T3> other) : base(other)
        {
            this.callback = other.callback;
        }

        public override SGRule Copy()
        {
            var n = new SGGenerator<T1, T2, T3>(token, callback);
            n.parent = parent;
            n.depth = depth;
            parameters.CopyTo(n.parameters, 0);
            return n;
        }

        public override void Call()
        {
            callback(parent, (T1)parameters[0], (T2)parameters[1], (T3)parameters[2]);
        }
    }
}