## PerMonitorDpi

Enable Windows 8.1+ Per-Monitor DPI support for Desktop WPF Apps. Instead of attempting to understand [this long MSDN article](http://msdn.microsoft.com/en-us/library/windows/desktop/ee308410(v=vs.85).aspx), take advantage of my personal suffering and use this instead:

```sh
Install-Package PerMonitorDpi
```

### How to Use

```cs
public MainWindow()
{
    new PerMonitorDpiBehavior(this);
}
```

To observe the difference, attach a normal monitor to a Surface Pro 2 or other Retina-DPI monitor, then move your window between the two monitors. Per-Monitor DPI apps will stay sharp, normal apps will have blurred text on the Retina monitor.

### What happens on older versions of Windows?

The Right Thing™ :)  Older versions of Windows will use the system-wide DPI information instead.
