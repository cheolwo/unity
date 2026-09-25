using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public class 한반도물류시각표본Tests
    {
        [TestCase(0)][TestCase(20)][TestCase(40)] public void 구면위에존재(float t)=>Assert.AreEqual(한반도물류시각표본View.Radius,한반도물류시각표본View.Position(t).magnitude,.00001f);
        [Test] public void 도착후왕복없음()=>Assert.AreEqual(한반도물류시각표본View.Position(40),한반도물류시각표본View.Position(400));
    }
}
