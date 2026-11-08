using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;
using Xunit;

namespace Nexo.Api.Tests;

public sealed class EndpointHelpersTests
{
    [Theory]
    [InlineData("https://github.com/algum-repositorio", true)]
    [InlineData("http://exemplo.org/arquivo", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/segredo", false)]
    [InlineData("uma url malformada", false)]
    public void ValidaUrlExterna(string url, bool expected) =>
        Assert.Equal(expected, EndpointHelpers.IsHttpLink(url));

    [Theory]
    [InlineData("Pesquisa", 150, true)]
    [InlineData("", 150, false)]
    [InlineData("  ", 150, false)]
    public void ValidaTextoObrigatorio(string value, int max, bool expected) =>
        Assert.Equal(expected, EndpointHelpers.IsValid(value, max));

    [Fact]
    public void ProjetosEncerradosNaoDevemSerEditados()
    {
        Assert.False(EndpointHelpers.IsEditable(new ResearchProject { Status = ProjectState.Completed }));
        Assert.False(EndpointHelpers.IsEditable(new ResearchProject { Status = ProjectState.Cancelled }));
        Assert.True(EndpointHelpers.IsEditable(new ResearchProject { Status = ProjectState.Proposed }));
    }
}
