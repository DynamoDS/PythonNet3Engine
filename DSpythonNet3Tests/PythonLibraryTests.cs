using DSPythonNet3;
using System.Collections;

namespace DSPythonNet3Tests
{
    public class PythonLibraryTests
    {
        [Test]
        public void TestSciPyAvailable()
        {
            string code = @"
from scipy import special
OUT = special.round(3.3333333)
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3.0));
        }

        [Test]
        public void TestContourPyAvailable()
        {
            string code = @"
import numpy as np
import contourpy

x = np.linspace(-2, 2, 20)
y = np.linspace(-2, 2, 20)
X, Y = np.meshgrid(x, y)
Z = np.sin(X) * np.cos(Y)

contour_generator = contourpy.contour_generator(x, y, Z)
lines = contour_generator.lines(0.0)
OUT = len(lines)
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void TestCyclerAvailable()
        {
            string code = @"
from cycler import cycler
color_cycle = cycler(color=['r', 'g', 'b'])
OUT = len(color_cycle)
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void TestEt_xmlfileAvailable()
        {
            string code = @"
from io import BytesIO
from xml.etree.ElementTree import Element
from et_xmlfile import xmlfile

out = BytesIO()
with xmlfile(out) as xf:
    el = Element(""root"")
    xf.write(el)

OUT = out.getvalue() == b""<root />""
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(true));
        }

        [Test]
        public void TestFonttoolsAvailable()
        {
            string code = @"
from fontTools.pens import svgPathPen
pen = svgPathPen.SVGPathPen(None)
pen.moveTo((0, 0))
pen.lineTo((1, 1))
OUT = pen.getCommands()
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo("M0 0 1 1"));
        }

        [Test]
        public void TestKiwisolverAvailable()
        {
            string code = @"
import kiwisolver as kiwi

solver = kiwi.Solver()
x = kiwi.Variable('x')
solver.addConstraint(3 * x + 5 == 14)
solver.updateVariables()
OUT = x.value()
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void TestMatplotlibAvailable()
        {
            string code = @"
import matplotlib as mpl

cmap = mpl.colormaps['plasma']
OUT = cmap.name
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo("plasma"));
        }

        [Test]
        public void TestPillowAvailable()
        {
            string code = @"
from PIL import Image, ImageDraw

image = Image.new('RGB', (100, 100), 'white')
draw = ImageDraw.Draw(image)
draw.rectangle((25, 25, 100, 100), fill='red')
OUT = image.getpixel((50, 50))[0]
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(255));
        }

        [Test]
        public void TestPyParsingAvailable()
        {
            string code = @"
import pyparsing as pp

parser = pp.Word(pp.alphas) + ',' + pp.Word(pp.alphas)
parsed = parser.parse_string('Hello, World')
OUT = len(parsed)
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void TestPythonDateutilAvailable()
        {
            string code = @"
from dateutil.parser import parse

date_string = 'Today is January 3, 2047 at 8:21:00AM'
parsed_date = parse(date_string, fuzzy=True)
OUT = parsed_date.day
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void TestPandasAvailable()
        {
            string code = @"
import pandas as pd

ser = pd.Series(range(5), index=list('abcde'))
OUT = ser['d'].item()
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void TestOpenpyxlAvailable()
        {
            string code = @"
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
treeData = [['A', 'B', 'C'], [1, 2, 3]]
for row in treeData:
  ws.append(row)
OUT = ws['C2'].value
";
            var empty = new ArrayList();
            var result = DSPythonNet3Evaluator.EvaluatePythonScript(code, empty, empty);
            Assert.That(result, Is.EqualTo(3));
        }
    }
}
