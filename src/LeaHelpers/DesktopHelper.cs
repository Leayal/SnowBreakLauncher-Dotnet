using Avalonia.Controls;
using Avalonia;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using System.Runtime.CompilerServices;

namespace Leayal.SnowBreakLauncher.LeaHelpers
{
    static class DesktopHelper
    {
        private readonly static DesktopHelperImpl a;
        private static bool TryLoadLibX11([NotNullWhen(true)] out SafeLibraryHandle libraryHandle)
        {
            libraryHandle = SafeLibraryHandle.Invalid;
            nint nativeLibrary;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && NativeLibrary.TryLoad("libX11.so.6", out nativeLibrary))
            {
                libraryHandle = new SafeLibraryHandle(nativeLibrary);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && NativeLibrary.TryLoad("libX11.6.dylib", out nativeLibrary))
            {
                libraryHandle = new SafeLibraryHandle(nativeLibrary);
            }
            else if (NativeLibrary.TryLoad("libX11", out nativeLibrary))
            {
                libraryHandle = new SafeLibraryHandle(nativeLibrary);
            }
            return (libraryHandle != null);
        }

        abstract class DesktopHelperImpl
        {
            public abstract void GetScreenSize(out int width, out int height);
        }

        sealed class DesktopHelperImpl_Windows : DesktopHelperImpl
        {
            public override void GetScreenSize(out int width, out int height)
            {
                width = PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
                height = PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
            }
        }

        sealed class DesktopHelperImpl_Linux_WithX11Desktop : DesktopHelperImpl
        {
            private readonly SafeLibraryHandle lib;

            private unsafe delegate Display* XOpenDisplay(sbyte* param0);
            private unsafe delegate Screen* XDefaultScreenOfDisplay(Display* param0);

            private readonly XOpenDisplay Invocation_XOpenDisplay;
            private readonly XDefaultScreenOfDisplay Invocation_XDefaultScreenOfDisplay;

            public DesktopHelperImpl_Linux_WithX11Desktop()
            {
                if (!TryLoadLibX11(out this.lib)) throw new NotSupportedException();
                AppDomain.CurrentDomain.ProcessExit += this.CurrentDomain_ProcessExit;

                var addr_XOpenDisplay = this.lib.GetExport("XOpenDisplay");
                var addr_XDefaultScreenOfDisplay = this.lib.GetExport("DefaultScreenOfDisplay");

                this.Invocation_XOpenDisplay = Marshal.GetDelegateForFunctionPointer<XOpenDisplay>(addr_XOpenDisplay);
                this.Invocation_XDefaultScreenOfDisplay = Marshal.GetDelegateForFunctionPointer<XDefaultScreenOfDisplay>(addr_XDefaultScreenOfDisplay);
            }

            public override unsafe void GetScreenSize(out int width, out int height)
            {
                Display* d = this.Invocation_XOpenDisplay((sbyte*)Unsafe.AsPointer<sbyte>(ref Unsafe.NullRef<sbyte>()));
                Screen* s = this.Invocation_XDefaultScreenOfDisplay(d);
                ref readonly var screen = ref Unsafe.AsRef<Screen>(s);
                width = screen.width;
                height = screen.height;
            }

            private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
            {
                AppDomain.CurrentDomain.ProcessExit -= this.CurrentDomain_ProcessExit;
                this.lib.Dispose();
            }

            public unsafe readonly struct Screen
            {
                public readonly XExtData* ext_data;
                public readonly Display* display;
                public readonly Window root;
                public readonly int width;
                public readonly int height;
                public readonly int mwidth;
                public readonly int mheight;
                public readonly int ndepths;
                public readonly Depth* depths;
                public readonly int root_depth;
                public readonly Visual* root_visual;
                public readonly GC default_gc;
                public readonly Colormap cmap;
                public readonly nuint white_pixel;
                public readonly nuint black_pixel;
                public readonly int max_maps;
                public readonly int min_maps;
                public readonly int backing_store;
                public readonly int save_unders;
                public readonly nint root_input_mask;
            }

            public readonly unsafe partial struct Colormap : IComparable, IComparable<Colormap>, IEquatable<Colormap>, IFormattable
            {
                public readonly void* Value;

                public Colormap(void* value)
                {
                    Value = value;
                }

                public static Colormap NULL => new Colormap(null);

                public static bool operator ==(Colormap left, Colormap right) => left.Value == right.Value;

                public static bool operator !=(Colormap left, Colormap right) => left.Value != right.Value;

                public static bool operator <(Colormap left, Colormap right) => left.Value < right.Value;

                public static bool operator <=(Colormap left, Colormap right) => left.Value <= right.Value;

                public static bool operator >(Colormap left, Colormap right) => left.Value > right.Value;

                public static bool operator >=(Colormap left, Colormap right) => left.Value >= right.Value;

                public static explicit operator Colormap(void* value) => new Colormap(value);

                public static implicit operator void*(Colormap value) => value.Value;

                public static explicit operator Colormap(byte value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator byte(Colormap value) => (byte)(value.Value);

                public static explicit operator Colormap(short value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator short(Colormap value) => (short)(value.Value);

                public static explicit operator Colormap(int value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator int(Colormap value) => (int)(value.Value);

                public static explicit operator Colormap(long value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator long(Colormap value) => (long)(value.Value);

                public static explicit operator Colormap(nint value) => new Colormap(unchecked((void*)(value)));

                public static implicit operator nint(Colormap value) => (nint)(value.Value);

                public static explicit operator Colormap(sbyte value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator sbyte(Colormap value) => (sbyte)(value.Value);

                public static explicit operator Colormap(ushort value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator ushort(Colormap value) => (ushort)(value.Value);

                public static explicit operator Colormap(uint value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator uint(Colormap value) => (uint)(value.Value);

                public static explicit operator Colormap(ulong value) => new Colormap(unchecked((void*)(value)));

                public static explicit operator ulong(Colormap value) => (ulong)(value.Value);

                public static explicit operator Colormap(nuint value) => new Colormap(unchecked((void*)(value)));

                public static implicit operator nuint(Colormap value) => (nuint)(value.Value);

                public int CompareTo(object? obj)
                {
                    if (obj is Colormap other)
                    {
                        return CompareTo(other);
                    }

                    return (obj is null) ? 1 : throw new ArgumentException("obj is not an instance of Colormap.");
                }

                public int CompareTo(Colormap other) => ((nuint)(Value)).CompareTo((nuint)(other.Value));

                public override bool Equals(object? obj) => (obj is Colormap other) && Equals(other);

                public bool Equals(Colormap other) => ((nuint)(Value)).Equals((nuint)(other.Value));

                public override int GetHashCode() => ((nuint)(Value)).GetHashCode();

                public override string ToString() => ((nuint)(Value)).ToString((sizeof(nint) == 4) ? "X8" : "X16");

                public string ToString(string? format, IFormatProvider? formatProvider) => ((nuint)(Value)).ToString(format, formatProvider);
            }

            public unsafe partial struct Depth
            {
                public int depth;
                public int nvisuals;
                public Visual* visuals;
            }

            public readonly unsafe partial struct GC : IComparable, IComparable<GC>, IEquatable<GC>, IFormattable
            {
                public readonly void* Value;

                public GC(void* value)
                {
                    Value = value;
                }

                public static GC NULL => new GC(null);

                public static bool operator ==(GC left, GC right) => left.Value == right.Value;

                public static bool operator !=(GC left, GC right) => left.Value != right.Value;

                public static bool operator <(GC left, GC right) => left.Value < right.Value;

                public static bool operator <=(GC left, GC right) => left.Value <= right.Value;

                public static bool operator >(GC left, GC right) => left.Value > right.Value;

                public static bool operator >=(GC left, GC right) => left.Value >= right.Value;

                public static explicit operator GC(void* value) => new GC(value);

                public static implicit operator void*(GC value) => value.Value;

                public static explicit operator GC(byte value) => new GC(unchecked((void*)(value)));

                public static explicit operator byte(GC value) => (byte)(value.Value);

                public static explicit operator GC(short value) => new GC(unchecked((void*)(value)));

                public static explicit operator short(GC value) => (short)(value.Value);

                public static explicit operator GC(int value) => new GC(unchecked((void*)(value)));

                public static explicit operator int(GC value) => (int)(value.Value);

                public static explicit operator GC(long value) => new GC(unchecked((void*)(value)));

                public static explicit operator long(GC value) => (long)(value.Value);

                public static explicit operator GC(nint value) => new GC(unchecked((void*)(value)));

                public static implicit operator nint(GC value) => (nint)(value.Value);

                public static explicit operator GC(sbyte value) => new GC(unchecked((void*)(value)));

                public static explicit operator sbyte(GC value) => (sbyte)(value.Value);

                public static explicit operator GC(ushort value) => new GC(unchecked((void*)(value)));

                public static explicit operator ushort(GC value) => (ushort)(value.Value);

                public static explicit operator GC(uint value) => new GC(unchecked((void*)(value)));

                public static explicit operator uint(GC value) => (uint)(value.Value);

                public static explicit operator GC(ulong value) => new GC(unchecked((void*)(value)));

                public static explicit operator ulong(GC value) => (ulong)(value.Value);

                public static explicit operator GC(nuint value) => new GC(unchecked((void*)(value)));

                public static implicit operator nuint(GC value) => (nuint)(value.Value);

                public int CompareTo(object? obj)
                {
                    if (obj is GC other)
                    {
                        return CompareTo(other);
                    }

                    return (obj is null) ? 1 : throw new ArgumentException("obj is not an instance of GC.");
                }

                public int CompareTo(GC other) => ((nuint)(Value)).CompareTo((nuint)(other.Value));

                public override bool Equals(object? obj) => (obj is GC other) && Equals(other);

                public bool Equals(GC other) => ((nuint)(Value)).Equals((nuint)(other.Value));

                public override int GetHashCode() => ((nuint)(Value)).GetHashCode();

                public override string ToString() => ((nuint)(Value)).ToString((sizeof(nint) == 4) ? "X8" : "X16");

                public string ToString(string? format, IFormatProvider? formatProvider) => ((nuint)(Value)).ToString(format, formatProvider);
            }

            public unsafe partial struct Visual
            {
                public XExtData* ext_data;
                public VisualID visualid;
                public int c_class;
                public nuint red_mask;
                public nuint green_mask;
                public nuint blue_mask;
                public int bits_per_rgb;
                public int map_entries;
            }

            public readonly unsafe partial struct VisualID : IComparable, IComparable<VisualID>, IEquatable<VisualID>, IFormattable
            {
                public readonly void* Value;

                public VisualID(void* value)
                {
                    Value = value;
                }

                public static VisualID NULL => new VisualID(null);

                public static bool operator ==(VisualID left, VisualID right) => left.Value == right.Value;

                public static bool operator !=(VisualID left, VisualID right) => left.Value != right.Value;

                public static bool operator <(VisualID left, VisualID right) => left.Value < right.Value;

                public static bool operator <=(VisualID left, VisualID right) => left.Value <= right.Value;

                public static bool operator >(VisualID left, VisualID right) => left.Value > right.Value;

                public static bool operator >=(VisualID left, VisualID right) => left.Value >= right.Value;

                public static explicit operator VisualID(void* value) => new VisualID(value);

                public static implicit operator void*(VisualID value) => value.Value;

                public static explicit operator VisualID(byte value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator byte(VisualID value) => (byte)(value.Value);

                public static explicit operator VisualID(short value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator short(VisualID value) => (short)(value.Value);

                public static explicit operator VisualID(int value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator int(VisualID value) => (int)(value.Value);

                public static explicit operator VisualID(long value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator long(VisualID value) => (long)(value.Value);

                public static explicit operator VisualID(nint value) => new VisualID(unchecked((void*)(value)));

                public static implicit operator nint(VisualID value) => (nint)(value.Value);

                public static explicit operator VisualID(sbyte value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator sbyte(VisualID value) => (sbyte)(value.Value);

                public static explicit operator VisualID(ushort value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator ushort(VisualID value) => (ushort)(value.Value);

                public static explicit operator VisualID(uint value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator uint(VisualID value) => (uint)(value.Value);

                public static explicit operator VisualID(ulong value) => new VisualID(unchecked((void*)(value)));

                public static explicit operator ulong(VisualID value) => (ulong)(value.Value);

                public static explicit operator VisualID(nuint value) => new VisualID(unchecked((void*)(value)));

                public static implicit operator nuint(VisualID value) => (nuint)(value.Value);

                public int CompareTo(object? obj)
                {
                    if (obj is VisualID other)
                    {
                        return CompareTo(other);
                    }

                    return (obj is null) ? 1 : throw new ArgumentException("obj is not an instance of VisualID.");
                }

                public int CompareTo(VisualID other) => ((nuint)(Value)).CompareTo((nuint)(other.Value));

                public override bool Equals(object? obj) => (obj is VisualID other) && Equals(other);

                public bool Equals(VisualID other) => ((nuint)(Value)).Equals((nuint)(other.Value));

                public override int GetHashCode() => ((nuint)(Value)).GetHashCode();

                public override string ToString() => ((nuint)(Value)).ToString((sizeof(nint) == 4) ? "X8" : "X16");

                public string ToString(string? format, IFormatProvider? formatProvider) => ((nuint)(Value)).ToString(format, formatProvider);
            }

            /// <remarks>https://github.com/terrafx/terrafx.interop.xlib/blob/main/sources/Interop/Xlib/X11/other/helper-types/Window.cs</remarks>
            public readonly unsafe partial struct Window : IComparable, IComparable<Window>, IEquatable<Window>, IFormattable
            {
                public readonly void* Value;

                public Window(void* value)
                {
                    Value = value;
                }

                public static Window NULL => new Window(null);

                public static bool operator ==(Window left, Window right) => left.Value == right.Value;

                public static bool operator !=(Window left, Window right) => left.Value != right.Value;

                public static bool operator <(Window left, Window right) => left.Value < right.Value;

                public static bool operator <=(Window left, Window right) => left.Value <= right.Value;

                public static bool operator >(Window left, Window right) => left.Value > right.Value;

                public static bool operator >=(Window left, Window right) => left.Value >= right.Value;

                public static explicit operator Window(void* value) => new Window(value);

                public static implicit operator void*(Window value) => value.Value;

                public static explicit operator Window(byte value) => new Window(unchecked((void*)(value)));

                public static explicit operator byte(Window value) => (byte)(value.Value);

                public static explicit operator Window(short value) => new Window(unchecked((void*)(value)));

                public static explicit operator short(Window value) => (short)(value.Value);

                public static explicit operator Window(int value) => new Window(unchecked((void*)(value)));

                public static explicit operator int(Window value) => (int)(value.Value);

                public static explicit operator Window(long value) => new Window(unchecked((void*)(value)));

                public static explicit operator long(Window value) => (long)(value.Value);

                public static explicit operator Window(nint value) => new Window(unchecked((void*)(value)));

                public static implicit operator nint(Window value) => (nint)(value.Value);

                public static explicit operator Window(sbyte value) => new Window(unchecked((void*)(value)));

                public static explicit operator sbyte(Window value) => (sbyte)(value.Value);

                public static explicit operator Window(ushort value) => new Window(unchecked((void*)(value)));

                public static explicit operator ushort(Window value) => (ushort)(value.Value);

                public static explicit operator Window(uint value) => new Window(unchecked((void*)(value)));

                public static explicit operator uint(Window value) => (uint)(value.Value);

                public static explicit operator Window(ulong value) => new Window(unchecked((void*)(value)));

                public static explicit operator ulong(Window value) => (ulong)(value.Value);

                public static explicit operator Window(nuint value) => new Window(unchecked((void*)(value)));

                public static implicit operator nuint(Window value) => (nuint)(value.Value);

                public int CompareTo(object? obj)
                {
                    if (obj is Window other)
                    {
                        return CompareTo(other);
                    }

                    return (obj is null) ? 1 : throw new ArgumentException("obj is not an instance of Window.");
                }

                public int CompareTo(Window other) => ((nuint)(Value)).CompareTo((nuint)(other.Value));

                public override bool Equals(object? obj) => (obj is Window other) && Equals(other);

                public bool Equals(Window other) => ((nuint)(Value)).Equals((nuint)(other.Value));

                public override int GetHashCode() => ((nuint)(Value)).GetHashCode();

                public override string ToString() => ((nuint)(Value)).ToString((sizeof(nint) == 4) ? "X8" : "X16");

                public string ToString(string? format, IFormatProvider? formatProvider) => ((nuint)(Value)).ToString(format, formatProvider);
            }

            /// <remarks>https://github.com/terrafx/terrafx.interop.xlib/blob/main/sources/Interop/Xlib/X11/Xlib/Display.cs</remarks>
            public unsafe partial struct Display
            {
                public XExtData* ext_data;
                public IntPtr private1;
                public int fd;
                public int private2;
                public int proto_major_version;
                public int proto_minor_version;
                public sbyte* vendor;
                public XID private3;
                public XID private4;
                public XID private5;
                public int private6;
                public delegate* unmanaged<Display*, XID> resource_alloc;
                public int byte_order;
                public int bitmap_unit;
                public int bitmap_pad;
                public int bitmap_bit_order;
                public int nformats;
                public ScreenFormat* pixmap_format;
                public int private8;
                public int release;
                public IntPtr private9;
                public IntPtr private10;
                public int qlen;
                public nuint last_request_read;
                public nuint request;
                public sbyte* private11;
                public sbyte* private12;
                public sbyte* private13;
                public sbyte* private14;
                public uint max_request_size;
                public XrmHashBucket db;
                public delegate* unmanaged<Display*, int> private15;
                public sbyte* display_name;
                public int default_screen;
                public int nscreens;
                public Screen* screens;
                public nuint motion_buffer;
                public nuint private16;
                public int min_keycode;
                public int max_keycode;
                public sbyte* private17;
                public sbyte* private18;
                public int private19;
                public sbyte* xdefaults;
            }

            public readonly unsafe partial struct XrmHashBucket : IComparable, IComparable<XrmHashBucket>, IEquatable<XrmHashBucket>, IFormattable
            {
                public readonly void* Value;

                public XrmHashBucket(void* value)
                {
                    Value = value;
                }

                public static XrmHashBucket NULL => new XrmHashBucket(null);

                public static bool operator ==(XrmHashBucket left, XrmHashBucket right) => left.Value == right.Value;

                public static bool operator !=(XrmHashBucket left, XrmHashBucket right) => left.Value != right.Value;

                public static bool operator <(XrmHashBucket left, XrmHashBucket right) => left.Value < right.Value;

                public static bool operator <=(XrmHashBucket left, XrmHashBucket right) => left.Value <= right.Value;

                public static bool operator >(XrmHashBucket left, XrmHashBucket right) => left.Value > right.Value;

                public static bool operator >=(XrmHashBucket left, XrmHashBucket right) => left.Value >= right.Value;

                public static explicit operator XrmHashBucket(void* value) => new XrmHashBucket(value);

                public static implicit operator void*(XrmHashBucket value) => value.Value;

                public static explicit operator XrmHashBucket(byte value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator byte(XrmHashBucket value) => (byte)(value.Value);

                public static explicit operator XrmHashBucket(short value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator short(XrmHashBucket value) => (short)(value.Value);

                public static explicit operator XrmHashBucket(int value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator int(XrmHashBucket value) => (int)(value.Value);

                public static explicit operator XrmHashBucket(long value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator long(XrmHashBucket value) => (long)(value.Value);

                public static explicit operator XrmHashBucket(nint value) => new XrmHashBucket(unchecked((void*)(value)));

                public static implicit operator nint(XrmHashBucket value) => (nint)(value.Value);

                public static explicit operator XrmHashBucket(sbyte value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator sbyte(XrmHashBucket value) => (sbyte)(value.Value);

                public static explicit operator XrmHashBucket(ushort value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator ushort(XrmHashBucket value) => (ushort)(value.Value);

                public static explicit operator XrmHashBucket(uint value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator uint(XrmHashBucket value) => (uint)(value.Value);

                public static explicit operator XrmHashBucket(ulong value) => new XrmHashBucket(unchecked((void*)(value)));

                public static explicit operator ulong(XrmHashBucket value) => (ulong)(value.Value);

                public static explicit operator XrmHashBucket(nuint value) => new XrmHashBucket(unchecked((void*)(value)));

                public static implicit operator nuint(XrmHashBucket value) => (nuint)(value.Value);

                public int CompareTo(object? obj)
                {
                    if (obj is XrmHashBucket other)
                    {
                        return CompareTo(other);
                    }

                    return (obj is null) ? 1 : throw new ArgumentException("obj is not an instance of XrmHashBucket.");
                }

                public int CompareTo(XrmHashBucket other) => ((nuint)(Value)).CompareTo((nuint)(other.Value));

                public override bool Equals(object? obj) => (obj is XrmHashBucket other) && Equals(other);

                public bool Equals(XrmHashBucket other) => ((nuint)(Value)).Equals((nuint)(other.Value));

                public override int GetHashCode() => ((nuint)(Value)).GetHashCode();

                public override string ToString() => ((nuint)(Value)).ToString((sizeof(nint) == 4) ? "X8" : "X16");

                public string ToString(string? format, IFormatProvider? formatProvider) => ((nuint)(Value)).ToString(format, formatProvider);
            }

            public unsafe partial struct ScreenFormat
            {
                public XExtData* ext_data;
                public int depth;
                public int bits_per_pixel;
                public int scanline_pad;
            }

            public unsafe partial struct XExtData
            {
                public int number;
                public XExtData* next;
                public delegate* unmanaged<XExtData*, int> free_private;
                public sbyte* private_data;
            }

            public readonly unsafe partial struct XID : IComparable, IComparable<XID>, IEquatable<XID>, IFormattable
            {
                public readonly void* Value;

                public XID(void* value)
                {
                    Value = value;
                }

                public static XID NULL => new XID(null);

                public static bool operator ==(XID left, XID right) => left.Value == right.Value;

                public static bool operator !=(XID left, XID right) => left.Value != right.Value;

                public static bool operator <(XID left, XID right) => left.Value < right.Value;

                public static bool operator <=(XID left, XID right) => left.Value <= right.Value;

                public static bool operator >(XID left, XID right) => left.Value > right.Value;

                public static bool operator >=(XID left, XID right) => left.Value >= right.Value;

                public static explicit operator XID(void* value) => new XID(value);

                public static implicit operator void*(XID value) => value.Value;

                public static explicit operator XID(byte value) => new XID(unchecked((void*)(value)));

                public static explicit operator byte(XID value) => (byte)(value.Value);

                public static explicit operator XID(short value) => new XID(unchecked((void*)(value)));

                public static explicit operator short(XID value) => (short)(value.Value);

                public static explicit operator XID(int value) => new XID(unchecked((void*)(value)));

                public static explicit operator int(XID value) => (int)(value.Value);

                public static explicit operator XID(long value) => new XID(unchecked((void*)(value)));

                public static explicit operator long(XID value) => (long)(value.Value);

                public static explicit operator XID(nint value) => new XID(unchecked((void*)(value)));

                public static implicit operator nint(XID value) => (nint)(value.Value);

                public static explicit operator XID(sbyte value) => new XID(unchecked((void*)(value)));

                public static explicit operator sbyte(XID value) => (sbyte)(value.Value);

                public static explicit operator XID(ushort value) => new XID(unchecked((void*)(value)));

                public static explicit operator ushort(XID value) => (ushort)(value.Value);

                public static explicit operator XID(uint value) => new XID(unchecked((void*)(value)));

                public static explicit operator uint(XID value) => (uint)(value.Value);

                public static explicit operator XID(ulong value) => new XID(unchecked((void*)(value)));

                public static explicit operator ulong(XID value) => (ulong)(value.Value);

                public static explicit operator XID(nuint value) => new XID(unchecked((void*)(value)));

                public static implicit operator nuint(XID value) => (nuint)(value.Value);

                public int CompareTo(object? obj)
                {
                    if (obj is XID other)
                    {
                        return CompareTo(other);
                    }

                    return (obj is null) ? 1 : throw new ArgumentException("obj is not an instance of XID.");
                }

                public int CompareTo(XID other) => ((nuint)(Value)).CompareTo((nuint)(other.Value));

                public override bool Equals(object? obj) => (obj is XID other) && Equals(other);

                public bool Equals(XID other) => ((nuint)(Value)).Equals((nuint)(other.Value));

                public override int GetHashCode() => ((nuint)(Value)).GetHashCode();

                public override string ToString() => ((nuint)(Value)).ToString((sizeof(nint) == 4) ? "X8" : "X16");

                public string ToString(string? format, IFormatProvider? formatProvider) => ((nuint)(Value)).ToString(format, formatProvider);
            }
        }
    }
}
