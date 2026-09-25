using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public class 이천지구연속확대Tests
    {
        [TestCase(0,50)][TestCase(100,50)][TestCase(50,0)][TestCase(50,100)]
        public void 경계투명(int row,int column)=>Assert.AreEqual(0,이천지구연속확대View.EdgeOpacity(row,column));
        [Test] public void 내부불투명()=>Assert.AreEqual(1,이천지구연속확대View.EdgeOpacity(50,50));
        [Test] public void 대칭적단조증가()
        {
            float prior=0;
            for(int i=0;i<=8;i++){float value=이천지구연속확대View.EdgeOpacity(i,50);Assert.That(value,Is.InRange(prior,1f));Assert.AreEqual(value,이천지구연속확대View.EdgeOpacity(100-i,50));prior=value;}
        }
    }
}
