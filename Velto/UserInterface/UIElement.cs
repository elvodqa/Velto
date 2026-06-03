using System.ComponentModel;
using System.Text.Json;
using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto.UserInterface;

/*
1. Fit sizing widths
2. Grow & shrink sizing widths
3. wrap text
4. Fit sizing heights
5. Grow & shrink sizing heights
6. positions
7. draw commands 
*/

public enum SizeMode
{
    Fixed,
    Grow,
}

public enum LayoutDirection
{
    Horizontal, Vertical,
}

public class Size
{
    public int Value { get; set; } = 0;
    public SizeMode Mode { get; set; } = SizeMode.Grow;

    public Size()
    {
        
    }

    public Size(int value = 0, SizeMode mode = SizeMode.Grow)
    {
        Value = value;
        Mode = mode;
    }

    public override string ToString()
    {
        return $"{Mode}({Value})";
    }
}

public struct Padding
{
    public int Left { get; set; } = 32;
    public int Top { get; set; } = 32;
    public int Bottom { get; set; } = 32;
    public int Right { get; set; } = 32;
    
    public Padding(int left = 32, int top = 32, int bottom = 32, int right = 32)
    {
        Left = left;
        Top = top;
        Bottom = bottom;
        Right = right;
    }

    public Padding(int all = 32)
    {
        Left = all;
        Top = all;
        Bottom = all;
        Right = all;
    }
}

// use LayoutDirection when picking which axis to align
public enum Alignment
{
    TopOrLeft,
    Center,
    BottomOrRight,
}

public class UIElement
{
    public UIElement? Parent { get; set; }= null;
    public List<UIElement> Children { get; set; } = new();
    
    public int X { get; set; }
    public int Y { get; set; }

    public Size Width { get; set; } = new Size(0, SizeMode.Grow);
    public Size Height{ get; set; } = new Size(0, SizeMode.Grow);

    public Padding Padding { get; set; } = new Padding();
    public int ChildGap { get; set; } = 16;

    public int Min { get; set; } = 0;
    public int Max { get; set; } = 0;

    public Color4<Rgba> BackgroundColor { get; set; } = Color4.White;

    public LayoutDirection LayoutDirection { get; set; } = LayoutDirection.Horizontal;
    public Alignment Alignment { get; set; } = Alignment.TopOrLeft;
    public int RemainingWidth { get; set; }
    public int RemainingHeight { get; set; }
    
    public UIElement()
    {
        
    }
    
    public UIElement(int size, SizeMode mode = SizeMode.Fixed)
    {
        Width = new(size, mode);
        Height = new(size, mode);
    }
    
    public UIElement(Size width, Size height)
    {
        Width = width;
        Height = height;
    }
    
    public override string ToString()
    {
        return ToTreeString();
    }

    public string ToTreeString()
    {
        return ToTreeString(0);
    }

    private string ToTreeString(int depth)
    {
        var indent = new string(' ', depth * 2);

        var info =
            $"{indent}UIElement " +
            $"(X={X}, Y={Y}, W={Width}, H={Height}, Children={Children.Count})";

        if (Children == null || Children.Count == 0)
            return info;

        var childStrings = new List<string>();

        foreach (var child in Children)
        {
            childStrings.Add(child.ToTreeString(depth + 1));
        }

        return info + Environment.NewLine + string.Join(Environment.NewLine, childStrings);
    }
}

public static class Builder
{
    public static UIElement testContainer;
    
    public static void Test(Renderer r)
    {
        /*testContainer = new UIElement
        {
            X = 200,
            Y = 200,
            Width = new Size((int)r.WindowSizeInPixels.X - 400),
            Height = new Size((int)r.WindowSizeInPixels.Y - 400),
            LayoutDirection = LayoutDirection.Vertical,
            BackgroundColor = Color4.Purple,
            Padding = new Padding(32),
            Children = [
                new UIElement(),
                new UIElement(width: new Size(420, SizeMode.Fixed), height: new Size(120, SizeMode.Fixed)),
                new UIElement()
                {
                    Padding = new Padding(8),
                    Children = [
                        new UIElement(),
                        new UIElement(width: new Size(75, SizeMode.Fixed), height: new Size(62, SizeMode.Fixed)),
                        new UIElement(),
                    ]
                }
            ]
        };*/
        testContainer = new UIElement
        {
            X = 200,
            Y = 200,
            Width = new Size((int)r.WindowSizeInPixels.X - 400),
            Height = new Size((int)r.WindowSizeInPixels.Y - 400),
            LayoutDirection = LayoutDirection.Vertical,
            BackgroundColor = Color4.Purple,
            Padding = new Padding(32),
            Children = [
                new UIElement(),
                new UIElement(),
                new UIElement(),
            ]
        };

        testContainer.GrowChildElements(true);
        testContainer.ComputePositions(true);
        testContainer.GrowChildElements(false);
        testContainer.ComputePositions(false);
    }

    public static void Draw(Renderer r, UIElement e)
    {
        r.DrawRectangleBorder(e.X, e.Y, e.Width.Value, e.Height.Value, 3, new Vector4(1, 1, 1, 1)); 
        if (e.Children.Count == 0) return;
        foreach (var child in e.Children)
        {
            Draw(r, child);
        }
    }
    
    public static int ComputeFixedSize(this UIElement root, bool horizontal = true)
    {
        if (root.Children.Count == 0)
        {
            if (horizontal)
            {
                if (root.Width.Mode == SizeMode.Fixed) return root.Width.Value;
                if (root.Width.Mode == SizeMode.Grow) return 0;
            }
            else
            {
                if (root.Height.Mode == SizeMode.Fixed) return root.Height.Value;
                if (root.Height.Mode == SizeMode.Grow) return 0;
            }
        }
        
        // https://youtu.be/by9lQvpvMIc?si=zUlYUamHEvp_p2Hi&t=1182
        // have child but there is layout direction and axis mismatch
        if (root.LayoutDirection == LayoutDirection.Vertical)
        {
            if (horizontal)
            {
                var max = root.Children.Max(element => element.ComputeFixedSize(horizontal));
                return root.Padding.Right + root.Padding.Left + max;
            }
        }
        else // horizontal
        {
            if (!horizontal) // vert
            {
                var max = root.Children.Max(element => element.ComputeFixedSize(!horizontal));
                return root.Padding.Top + root.Padding.Bottom + max;
            }
        }
        
        // normal
        var size = 0;
        if (horizontal)
        {
            size += root.Padding.Left + root.Padding.Right;
        }
        else
        {
            size += root.Padding.Top + root.Padding.Bottom;
        }
        
        foreach (var child in root.Children)
        {
            size += ComputeFixedSize(child, horizontal);
        }
        
        if ((horizontal && root.Children.TrueForAll(e => e.Width.Mode == SizeMode.Fixed)) 
            || (!horizontal && root.Children.TrueForAll(e => e.Height.Mode == SizeMode.Fixed)))
        {
            if (root.Children.Count != 0) size += (root.Children.Count - 1) * root.ChildGap;
        }
        
        return size;
    }

    public static void GrowChildElements(this UIElement root, bool horizontal = true)
    {
        if (root.Children.Count == 0) return;
        
        if (horizontal)
        {
            var growCount = root.Children.Count(e => e.Width.Mode == SizeMode.Grow);
            //if (growCount == 0) return;
            
            float remainingWidth;
            if (root.Parent is null) remainingWidth = root.Width.Value;
            else remainingWidth = root.Width.Value;;
            
            remainingWidth -= (root.Padding.Left + root.Padding.Right);
            foreach (var child in root.Children)
            {
                remainingWidth -= child.Width.Value;
            }
            remainingWidth -= (root.Children.Count - 1) * root.ChildGap;
            
            foreach (var child in root.Children)
            {
                if (child.Width.Mode == SizeMode.Grow)
                {
                    child.Width.Value = (int)(remainingWidth / growCount);
                }
            }

            root.RemainingWidth = (int)remainingWidth;

            foreach (var child in root.Children)
            {
                GrowChildElements(child, horizontal);
            }
        }
        else
        {
            var growCount = root.Children.Count(e => e.Height.Mode == SizeMode.Grow);
            //if (growCount == 0) return;
        
            float remainingHeight;
            if (root.Parent is null) remainingHeight = root.Height.Value;
            else remainingHeight = root.Height.Value;;

            
            remainingHeight -= (root.Padding.Top + root.Padding.Bottom);
            foreach (var child in root.Children)
            {
                remainingHeight -= child.Height.Value;
            }
            remainingHeight -= (root.Children.Count - 1) * root.ChildGap;
        
            foreach (var child in root.Children)
            {
                if (child.Height.Mode == SizeMode.Grow)
                {
                    child.Height.Value = (int)(remainingHeight / growCount);
                }
            }
            
            root.RemainingHeight = (int)remainingHeight;
            
            foreach (var child in root.Children)
            {
                GrowChildElements(child, horizontal);
            }
        }
    }
        
    public static void ComputePositions(this UIElement root, bool horizontal = true)
    {
        if (root.Children.Count == 0)
        {
            return;
        }
        
        if (horizontal)
        {
            float xOffset = 0;
            if (root.Alignment == Alignment.BottomOrRight) xOffset += root.RemainingWidth;
            if (root.Alignment == Alignment.Center) xOffset += (float)root.RemainingWidth/2;
            xOffset += root.Padding.Left;

            int i = 1;
            foreach (var children in root.Children)
            {
                children.X = root.X + (int)xOffset;
                xOffset += children.Width.Value;

                if (i != root.Children.Count)
                {
                    xOffset += root.ChildGap;
                    i++;
                }
            }

            foreach (var child in root.Children)
            {
                child.ComputePositions(horizontal);
            }
        }
        else
        {
            float yOffset = 0;
            if (root.Alignment == Alignment.BottomOrRight) yOffset += root.RemainingHeight;
            if (root.Alignment == Alignment.Center) yOffset += (float)root.RemainingHeight/2;
            yOffset += root.Padding.Top;

            int i = 1;
            foreach (var children in root.Children)
            {
                children.Y = root.Y + (int)yOffset;
                yOffset += children.Height.Value;

                if (i != root.Children.Count)
                {
                    yOffset += root.ChildGap;
                    i++;
                }
            }

            foreach (var child in root.Children)
            {
                child.ComputePositions(horizontal);
            }
        }
    }


}