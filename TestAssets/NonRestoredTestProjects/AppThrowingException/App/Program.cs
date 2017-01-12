using System;

namespace AppThrowing
{
    class MyException : Exception
    {
        static void Main(string[] args)
        {
            throw new MyException();
        }
    }
}