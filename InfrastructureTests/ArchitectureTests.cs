using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NetArchTest.Rules;

namespace InfrastructureTests
{
    [TestFixture]
    public class ArchitectureTests
    {
        private readonly Assembly _uiAssembly;
        private readonly Assembly _infrastructureAssembly;

        public ArchitectureTests()
        {
            _uiAssembly = Assembly.Load("NetSdrClientApp");
            _infrastructureAssembly = Assembly.Load("EchoServer");
        }

        [Test]
        public void UI_Should_Not_Depend_On_Infrastructure()
        {
            var result = Types
                .InAssembly(_uiAssembly)
                .ShouldNot()
                .HaveDependencyOn(_infrastructureAssembly.GetName().Name)
                .GetResult();

            // Використовуємо Array.Empty<string>() замість new string[0]
            var failing = result.FailingTypeNames ?? Array.Empty<string>();

            Assert.That(result.IsSuccessful,
                $"UI шар не повинен залежати від Infrastructure. Порушення: ");
        }
        


        [Test]
        public void Infrastructure_Should_Not_Depend_On_UI()
        {
            var result = Types
                .InAssembly(_infrastructureAssembly)
                .ShouldNot()
                .HaveDependencyOn(_uiAssembly.GetName().Name)
                .GetResult();

            var failing = result.FailingTypeNames ?? Array.Empty<string>();

            Assert.That(result.IsSuccessful,
                $"Infrastructure не повинен залежати від UI. Порушення: ");
        }
    }
}
