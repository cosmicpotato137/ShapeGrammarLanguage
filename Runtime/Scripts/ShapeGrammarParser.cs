using cosmicpotato.parsergenerator;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace cosmicpotato.sgl
{
    public class ShapeGrammarParser
    {
        private Dictionary<string, SGGeneratorBase> generators;
        private Dictionary<string, SGProducer> producers;
        private Dictionary<string, SGVar> variables;
        private Dictionary<string, SGVar> prepVars;
        private Dictionary<string, Type> sgTypes;

        private SGProdGen opTree;

        private LinkedList<SGGeneratorBase> opQueue;

        private Parser<ELang> parser;

        public ShapeGrammarParser()
        {
            generators = new Dictionary<string, SGGeneratorBase>();
            producers = new Dictionary<string, SGProducer>();
            variables = new Dictionary<string, SGVar>();
            prepVars = new Dictionary<string, SGVar>();
            opQueue = new LinkedList<SGGeneratorBase>();

            // types for sg language
            sgTypes = new Dictionary<string, Type>();
            sgTypes.Add("string", typeof(string));
            sgTypes.Add("int", typeof(int));
            sgTypes.Add("sgrule", typeof(SGGeneratorBase));
            sgTypes.Add("sgprod", typeof(SGProdGen));
            sgTypes.Add("sggen", typeof(SGProdGen));

            //SGVar depth = new SGVar("maxDepth", -1);
            //prepVars.Add(depth.token, depth);
            //SGVar oper = new SGVar("maxOper", -1);
            //prepVars.Add(oper.token, oper);
            //SGVar seed = new SGVar("seed", -1);
            //prepVars.Add(seed.token, seed);
        }

        // parse a text file
        public SGRoot Parse(TextAsset text)
        {
            producers.Clear();
            variables.Clear();

            //prepVars["seed"].Set(-1);
            //prepVars["maxOper"].Set(-1);
            //prepVars["maxDepth"].Set(-1);

            var pr = parser.Parse<SGRoot>(text.text);
            if (!pr.Success)
            {
                Debug.LogError("Parsing failed:");
                foreach (ErrorInfo i in pr.Errors)
                {
                    Debug.LogError(i.Description);
                }
                return null;
            }

            SGRoot root = pr.Value;
            root.Init(null);
            root.Produce(null, 0, 10, 10000);

            return pr.Value;
        }

        // run the currently parsed shape grammar
        public void RunShapeGrammar(int maxDepth, int maxOper = 100000, Transform parent = null)
        {
            opQueue.Clear();

            if (prepVars.ContainsKey("maxDepth") && prepVars["maxDepth"].Get<int>() > 0)
                maxDepth = prepVars["maxDepth"].Get<int>();
            if (prepVars.ContainsKey("maxOper") && prepVars["maxOper"].Get<int>() > 0)
                maxOper = prepVars["maxOper"].Get<int>();
            if (prepVars.ContainsKey("seed") && prepVars["seed"].Get<int>() > 0)
                SGProducer.rg = new System.Random(prepVars["seed"].Get<int>());

            SGProducer.rg = new System.Random();

            //SGGeneratorBase.maxDepth = maxDepth;
            if (parent == null)
                opTree.scope = Scope.identity;
            else
                opTree.scope = new Scope(parent.position, parent.rotation, Vector3.one);
            opQueue.AddLast(opTree);

            int i = 0;
            opQueue.First.Value.Generate();
            while (opQueue.Count > 0 && i < maxOper)
            {
                opQueue.RemoveFirst();
                i++;
            }
        }

        // add generator to the list of expected generators
        public void AddGenerator(SGGeneratorBase rule)
        {
            generators.Add(rule.token, rule);
        }

        // add producer to the list of expected producers
        public void AddProducer(SGProducer producer)
        {
            producers.Add(producer.token, producer);
        }

        // check if name exists in any context
        public bool NameExsists(string token)
        {
            return producers.ContainsKey(token) || generators.ContainsKey(token) ||
                variables.ContainsKey(token) || prepVars.ContainsKey(token) ||
                sgTypes.ContainsKey(token);
        }

        // enum of all symbols to expect in the grammar
        private enum ELang
        {
            // production rules
            START, Rule, GenRuleList, GenRule, ExpList, Exp, ProdRule, ProdRuleList,
            Var, VarList, Array, GenRuleLists,
            // symbols
            Ignore, LParen, RParen, Number, Name, RArrow, Colon, Comma, Break,
            Pound, Equals, LCBrac, RCBrac, LBrac, RBrac, String, Star, Quote
        }

        public void CompileParser()
        {
            // list of regular expressions matching tokens
            var tokens = new LexerDefinition<ELang>(new Dictionary<ELang, TokenRegex>
            {
                [ELang.Ignore] = "[\\s\\n]+",
                [ELang.Name] = @"[A-Za-z_][a-zA-Z0-9_]*",
                [ELang.String] = "\"[^\"]*\"", // todo: fix this
                [ELang.LParen] = "\\(",
                [ELang.RParen] = "\\)",
                [ELang.Number] = "[-+]?\\d*(\\.\\d+)?",
                [ELang.RArrow] = @"->",
                [ELang.Colon] = @":",
                [ELang.Comma] = @",",
                [ELang.Break] = @"%%",
                [ELang.Pound] = "\\#",
                [ELang.Equals] = @"=",
                [ELang.LCBrac] = "{",
                [ELang.RCBrac] = "}",
                [ELang.LBrac] = "\\[",
                [ELang.RBrac] = "\\]",
                [ELang.Star] = "\\*",
            });

            var grammarRules = new GrammarRules<ELang>(new Dictionary<ELang, Token[][]>()
            {
                [ELang.START] = new Token[][]
                {
                new Token[] { ELang.ProdRuleList, new Op(o => o[0] = new SGRoot(o[0])) },
                new Token[] { ELang.VarList, ELang.Break, ELang.ProdRuleList, new Op(o => o[0] = new SGRoot(o[2])) }
                },
                // list of production rules
                [ELang.ProdRuleList] = new Token[][]
                {
                new Token[] { ELang.ProdRule,
                    new Op(o =>
                    {
                        //SGProducer.opQueue = this.opQueue;
                        //SGProducer p = o[0];
                        //SGProdGen pg = new SGProdGen("__BEGIN__", () => p);
                        //opTree = pg;
                        //opTree.scope = Scope.identity.Copy();
                        //opTree.depth = 0;
                        o[0] = new List<SGProducer>{ o[0] };
                    })
                },
                new Token[] { ELang.ProdRuleList, ELang.ProdRule,
                    new Op(o =>
                    {
                        List<SGProducer> lst = o[0];
                        lst.Add((SGProducer)o[1]);
                        o[0] = lst;
                    })
                }
                },
                // matches a production rule and adds a producer to producers
                [ELang.ProdRule] = new Token[][]
                {
                new Token[] { ELang.Name, ELang.LParen, ELang.RParen, ELang.Colon, ELang.LCBrac, ELang.GenRuleList, ELang.RCBrac,
                    new Op(o =>
                    {
                        o[0] = new SGProducer(o[0], o[5]);
                    })
                },
                new Token[] { ELang.Name, ELang.LParen, ELang.RParen, ELang.Colon, ELang.GenRuleLists,
                    new Op(o =>
                    {
                        //AddProducer(p);
                        var ruleLists = new List<LinkedList<SGGeneratorBase>>();
                        List<SGExp> pList = new List<SGExp>();
                        foreach (Tuple<SGExp, LinkedList<SGGeneratorBase>> pair in o[4])
                        {
                            pList.Add(pair.Item1);
                            ruleLists.Add(pair.Item2);
                        }
                        o[0] = new SGProducer(o[0], ruleLists, pList);
                    })
                },
                new Token[] { ELang.Name, ELang.LParen, ELang.ExpList, ELang.RParen, ELang.Colon, ELang.LCBrac, ELang.GenRuleList, ELang.RCBrac,
                    new Op(o =>
                    {
                        o[0] = new SGProducer(o[0], o[6], o[2]);
                    })
                },
                new Token[] { ELang.Name, ELang.LParen, ELang.ExpList, ELang.RParen, ELang.Colon, ELang.GenRuleLists,
                    new Op(o =>
                    {
                        var ruleLists = new List<LinkedList<SGGeneratorBase>>();
                        List<SGExp> pList = new List<SGExp>();
                        foreach (Tuple<SGExp, LinkedList<SGGeneratorBase>> pair in o[5])
                        {
                            pList.Add(pair.Item1);
                            ruleLists.Add(pair.Item2);
                        }
                        o[0] = new SGProducer((string)o[0], ruleLists, pList, o[2]);
                    })
                },
                },
                // matches one or more lists of generator rules in a production rule
                [ELang.GenRuleLists] = new Token[][]
                {
                new Token[] { ELang.LParen, ELang.Exp, ELang.RParen,
                    ELang.LCBrac, ELang.GenRuleList, ELang.RCBrac,
                    new Op(o =>
                    {
                        var lst = new LinkedList<Tuple<SGExp, LinkedList<SGGeneratorBase>>>();
                        lst.AddFirst(new Tuple<SGExp, LinkedList<SGGeneratorBase>>(o[1], o[4]));
                        o[0] = lst;
                    })
                },
                new Token[] { ELang.LParen, ELang.Exp, ELang.RParen,
                    ELang.LCBrac, ELang.GenRuleList, ELang.RCBrac, ELang.GenRuleLists,
                    new Op(o =>
                    {
                        o[6].AddFirst(new Tuple<SGExp, LinkedList<SGGeneratorBase>>(o[1], o[4]));
                        o[0] = o[6];
                    })
                }
                },
                // list of generator rules
                [ELang.GenRuleList] = new Token[][]
                {
                new Token[] { ELang.GenRule,
                    new Op(o =>
                    {
                        var lst = new LinkedList<SGGeneratorBase>();
                        lst.AddFirst(o[0]);
                        o[0] = lst;
                    })
                },
                new Token[] { ELang.GenRule, ELang.GenRuleList,
                    new Op(o =>
                    {
                        o[1].AddFirst(o[0]);
                        o[0] = o[1];
                    })
                }
                },
                [ELang.GenRule] = new Token[][] {
                // function with no parameters
                new Token[] { ELang.Name, ELang.LParen, ELang.RParen,
                    new Op(o =>
                    {

                        if (generators.ContainsKey(o[0]))
                            o[0] = generators[o[0]].Copy();
                        else
                        {
                            // find the producer corresponding to SGProdGen
                            //string name = o[0];
                            //Func<SGProducer> findProd = () =>
                            //{
                            //    if (producers.ContainsKey(name))
                            //        return producers[name];
                            //    else
                            //        throw new MissingMethodException($"Shape grammar rule: {name} does not exist");
                            //};
                            o[0] = new SGProdGen(o[0], new List<SGExp>());
                        }
                    })
                },
                // function with parameters
                new Token[] { ELang.Name, ELang.LParen, ELang.ExpList, ELang.RParen,
                    new Op(o =>
                    {
                        if (generators.ContainsKey(o[0]))
                        {
                            // add all parameters to the generator
                            var g = generators[o[0]].Copy();
                            List<SGExp> expList = o[2];

                            g.parameters = expList;
                            o[0] = g;
                        }
                        else
                        {
                            o[0] = new SGProdGen(o[0], o[2]);
                        }
                    })
                }
                },

                // matches any list of expressions separated by commas
                [ELang.ExpList] = new Token[][]
                {
                new Token[] { ELang.Exp,
                    new Op(o =>
                    {
                        var lst = new List<SGExp>();
                        lst.Add(o[0]);
                        o[0] = lst;
                    })
                },
                new Token[] { ELang.ExpList, ELang.Comma, ELang.Exp,
                    new Op(o =>
                    {
                        o[0].Add(o[2]);
                    })
                }
                },
                // matches numbers, strings, generator rules, or lists
                [ELang.Exp] = new Token[][]
                {
                new Token[] { ELang.Number, new Op(o => o[0] = new SGNumber(o[0], (float)Convert.ToDouble(o[0]))) },
                new Token[] { ELang.String,
                    new Op(o =>
                    {
                        string s = Convert.ToString(o[0]);
                        o[0] = new SGString(o[0], s.Substring(1, s.Length - 2));
                    })
                },
                new Token[] { ELang.Name,
                    new Op(o =>
                    {
                        string v = Convert.ToString(o[0]);
                        o[0] = new SGInlineVar(o[0], v);
                    })
                },

                // TODO
                // typed list
                //new Token[] { ELang.Name, ELang.LCBrac, ELang.ExpList, ELang.RCBrac,
                //    new Op(o =>
                //    {
                //        if (!sgTypes.ContainsKey(o[0]))
                //            throw new TypeUnloadedException($"Type does not exist: {o[0]}");

                //        Type arrType = sgTypes[o[0]];
                //        LinkedList<dynamic> rules = o[2];
                //        foreach (dynamic rule in rules)
                //        {
                //            var typ = rule.GetType().BaseType;
                //            if (typ != typeof(SGGeneratorBase))
                //                throw new ArrayTypeMismatchException($"Unsupported list type: {rule.GetType()}");
                //        }
                //        o[0] = new SGGeneratorBase[rules.Count];
                //        rules.CopyTo(o[0], 0);
                //    })
                //}
                },

                // matches a list of variables
                [ELang.VarList] = new Token[][]
                {
                new Token[] { ELang.Var },
                new Token[] { ELang.VarList, ELang.Var }
                },
                // matches a single define or var
                [ELang.Var] = new Token[][]
                {
                new Token[] { ELang.Pound, "\\s*var\\s+", ELang.Name, ELang.Exp,
                    new Op(o =>
                    {
                        if (NameExsists(o[2]))
                        {
                            throw new Exception($"Name already defined: {o[2]}");
                        }
                        var v = new SGVar(o[2], o[3]);
                        variables.Add(v.token, v);
                    })
                },
                new Token[] { ELang.Pound, "\\s*define\\s+", ELang.Name, ELang.Exp,
                    new Op(o =>
                    {
                        if (prepVars.ContainsKey(o[2]))
                            prepVars[o[2]].Set(o[3]);
                        else
                            Debug.LogWarning($"Preprocessing var not found: {o[2]}");
                    })
                }
                }
            });

            // add rules and generate the grammar
            var lexer = new Lexer<ELang>(tokens, ELang.Ignore);
            parser = new ParserGenerator<ELang>(lexer, grammarRules).CompileParser();
        }
    }
}