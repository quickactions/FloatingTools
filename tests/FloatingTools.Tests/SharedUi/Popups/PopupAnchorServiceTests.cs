using System.Threading;
using System.Windows;
using FloatingTools.App.SharedUi.Popups;

namespace FloatingTools.Tests.SharedUi.Popups;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class PopupAnchorServiceTests
{
    [Fact]
    public void Show_OpensOneActiveHostAndRequest()
        => RunSta(() =>
        {
        var hosts = new List<FakeHost>();
        var service = CreateService(hosts);
        var request = CreateRequest();

        service.Show(request);

        Assert.True(service.IsOpen);
        Assert.Same(request, service.ActiveRequest);
        Assert.Single(hosts);
        Assert.True(hosts[0].IsOpen);
        service.Close();
        });

    [Fact]
    public void Show_SecondRequestReplacesFirstHost()
        => RunSta(() =>
        {
        var hosts = new List<FakeHost>();
        var service = CreateService(hosts);
        var first = CreateRequest();
        var second = CreateRequest();

        service.Show(first);
        service.Show(second);

        Assert.Equal(2, hosts.Count);
        Assert.False(hosts[0].IsOpen);
        Assert.Equal(1, hosts[0].CloseCount);
        Assert.True(hosts[1].IsOpen);
        Assert.Same(second, service.ActiveRequest);
        service.Close();
        });

    [Fact]
    public void Show_SingleInstanceClosesHostOwnedByAnotherService()
        => RunSta(() =>
        {
        var firstHosts = new List<FakeHost>();
        var secondHosts = new List<FakeHost>();
        var firstService = CreateService(firstHosts);
        var secondService = CreateService(secondHosts);

        firstService.Show(CreateRequest());
        secondService.Show(CreateRequest());

        Assert.False(firstService.IsOpen);
        Assert.Equal(1, firstHosts[0].CloseCount);
        Assert.True(secondService.IsOpen);
        secondService.Close();
        });

    [Fact]
    public void Close_ClosesActiveHostAndClearsRequest()
        => RunSta(() =>
        {
        var hosts = new List<FakeHost>();
        var service = CreateService(hosts);
        service.Show(CreateRequest());

        service.Close();

        Assert.False(service.IsOpen);
        Assert.Null(service.ActiveRequest);
        Assert.Equal(1, hosts[0].CloseCount);
        });

    [Fact]
    public void HostInitiatedClose_RaisesClosedAndClearsRequest()
        => RunSta(() =>
        {
            var hosts = new List<FakeHost>();
            var service = CreateService(hosts);
            var closeCount = 0;
            service.Closed += (_, _) => closeCount++;
            service.Show(CreateRequest());

            hosts[0].Close();

            Assert.Equal(1, closeCount);
            Assert.False(service.IsOpen);
            Assert.Null(service.ActiveRequest);
        });

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private static PopupAnchorService CreateService(List<FakeHost> hosts)
        => new(() =>
        {
            var host = new FakeHost();
            hosts.Add(host);
            return host;
        });

    private static PopupAnchorRequest CreateRequest()
        => new(new FrameworkElement(), new FrameworkElement(), new FrameworkElement());

    private sealed class FakeHost : IPopupAnchorHost
    {
        public event EventHandler? Closed;

        public bool IsOpen { get; private set; }

        public PopupAnchorRequest? Request { get; private set; }

        public int CloseCount { get; private set; }

        public void Show(PopupAnchorRequest request)
        {
            Request = request;
            IsOpen = true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CloseCount++;
            IsOpen = false;
            Request = null;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
