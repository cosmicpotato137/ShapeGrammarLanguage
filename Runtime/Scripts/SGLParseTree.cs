using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class SGLNode
{
    SGLNode parent;

    public void Init(SGLNode parent)
    {
        throw new NotImplementedException();
    }

    public void GetName(string name)
    {
        throw new NotImplementedException();
    }
}

class SGLExpList : SGLNode
{
    SGLExp val;
    SGLExpList child;
}

class SGLExp : SGLNode
{

}

class SGLProdRule : SGLNode
{

}

