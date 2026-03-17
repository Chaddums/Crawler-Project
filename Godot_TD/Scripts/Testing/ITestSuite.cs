using System.Threading.Tasks;

namespace JunkyardTD
{
    public interface ITestSuite
    {
        string SuiteName { get; }
        Task Run(TestContext ctx);
    }
}
