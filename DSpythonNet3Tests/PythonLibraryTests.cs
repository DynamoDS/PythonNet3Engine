using Dynamo;
using System.Collections;

namespace DSPythonNet3
{
    public class PythonLibraryTests : DynamoModelTestBase
    {
        protected override void GetLibrariesToPreload(List<string> libraries)
        {
            libraries.Add("DSCoreNodes.dll");
        }

        [Test]
        public void TestSciPyAvailable()
        {
            string code = @"
from scipy import special

OUT = special.exp10(3)
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(1000));
        }
    }
}