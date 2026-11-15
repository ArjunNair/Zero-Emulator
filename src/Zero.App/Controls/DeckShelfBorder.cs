using System;
using Avalonia;
using Avalonia.Controls;

namespace Zero.App.Controls
{
    // The shelf the decks sit on, docked under the screen. It gives the decks (in a Viewbox, which
    // scales them to the width it is offered) just enough width to come out as wide as the picture
    // above: the window's whole width, unless the window is too short for a picture that wide, and
    // then the picture and the decks shrink together.
    //
    // The scale is worked out here, in the measure pass, from the room the shelf is offered, so it
    // is right in every layout however the window came to be that size. Setting a size limit on
    // the Viewbox from the window's resize events instead left the decks at their old size on
    // macOS, where the resize lands in the middle of a layout pass.
    public sealed class DeckShelfBorder : Border
    {
        // Width over height of the picture as shown. Zero when the picture stretches to fill the
        // room it is given, and the decks then simply fill the window's width.
        public static readonly StyledProperty<double> PictureAspectProperty =
            AvaloniaProperty.Register<DeckShelfBorder, double>(nameof(PictureAspect), 352.0 / 296);
        public double PictureAspect { get => GetValue(PictureAspectProperty); set => SetValue(PictureAspectProperty, value); }

        // The decks are drawn at any size, but below this their legends cannot be read.
        public const double MinScale = 0.3;

        static DeckShelfBorder()
        {
            AffectsMeasure<DeckShelfBorder>(PictureAspectProperty);
        }

        // The scale for decks of this natural size, on a shelf offered this much room: the width of
        // the window, and the height left for the picture and the shelf between them.
        public double ScaleFor(Size room, Size natural)
        {
            Thickness pad = Padding + BorderThickness;
            double padX = pad.Left + pad.Right, padY = pad.Top + pad.Bottom;
            double scale = (room.Width - padX) / natural.Width;
            double aspect = PictureAspect;
            if (aspect > 0 && !double.IsInfinity(room.Height))
            {
                // The picture gets the height the shelf leaves, and is that height times its aspect
                // wide; this is the scale at which the shelf comes out exactly as wide.
                scale = Math.Min(scale, (aspect * (room.Height - padY) - padX) / (natural.Width + aspect * natural.Height));
            }
            return Math.Max(MinScale, scale);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Control child = Child;
            if (child == null || double.IsInfinity(availableSize.Width)) return base.MeasureOverride(availableSize);
            child.Measure(Size.Infinity);   // offered no limit, the Viewbox reports the decks' natural size
            Size natural = child.DesiredSize;
            if (natural.Width <= 0 || natural.Height <= 0) return base.MeasureOverride(availableSize);

            Thickness pad = Padding + BorderThickness;
            double width = natural.Width * ScaleFor(availableSize, natural) + pad.Left + pad.Right;
            return base.MeasureOverride(new Size(width, double.PositiveInfinity));
        }
    }
}
