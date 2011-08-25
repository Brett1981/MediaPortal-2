#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

// Define DEBUG_LAYOUT to make MP log screen layouting information. That will slow down the layouting process significantly
// but can be used to find layouting bugs. Don't use that switch in release builds.
// Use DEBUG_MORE_LAYOUT to get more information, also for skipped method calls.
//#define DEBUG_LAYOUT
//#define DEBUG_MORE_LAYOUT

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.Core;
using MediaPortal.Core.General;
using MediaPortal.UI.SkinEngine.Commands;
using MediaPortal.UI.SkinEngine.ContentManagement;
using MediaPortal.UI.SkinEngine.Controls.Transforms;
using MediaPortal.UI.SkinEngine.Fonts;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UI.SkinEngine.Rendering;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using SlimDX;
using SlimDX.Direct3D9;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.Controls.Visuals.Styles;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.UI.SkinEngine.Controls.Visuals
{
  public enum VerticalAlignmentEnum
  {
    Top = 0,
    Center = 1,
    Bottom = 2,
    Stretch = 3,
  };

  public enum HorizontalAlignmentEnum
  {
    Left = 0,
    Center = 1,
    Right = 2,
    Stretch = 3,
  };

  public enum MoveFocusDirection
  {
    Up,
    Down,
    Left,
    Right
  }

  public class SetElementStateAction : IUIElementAction
  {
    protected ElementState _state;

    public SetElementStateAction(ElementState state)
    {
      _state = state;
    }

    public void Execute(UIElement element)
    {
      element.ElementState = _state;
    }
  }

  public enum ElementState
  {
    Available,
    Running,
    Disposing
  }

  public class FrameworkElement : UIElement
  {
    public const string GOTFOCUS_EVENT = "FrameworkElement.GotFocus";
    public const string LOSTFOCUS_EVENT = "FrameworkElement.LostFocus";
    public const string MOUSEENTER_EVENT = "FrameworkElement.MouseEnter";
    public const string MOUSELEAVE_EVENT = "FrameworkElement.MouseLeave";

    protected const string GLOBAL_RENDER_TEXTURE_ASSET_KEY = "SkinEngine::GlobalRenderTarget";

    #region Protected fields

    protected AbstractProperty _widthProperty;
    protected AbstractProperty _heightProperty;

    protected AbstractProperty _actualWidthProperty;
    protected AbstractProperty _actualHeightProperty;
    protected AbstractProperty _minWidthProperty;
    protected AbstractProperty _minHeightProperty;
    protected AbstractProperty _maxWidthProperty;
    protected AbstractProperty _maxHeightProperty;
    protected AbstractProperty _horizontalAlignmentProperty;
    protected AbstractProperty _verticalAlignmentProperty;
    protected AbstractProperty _styleProperty;
    protected AbstractProperty _focusableProperty;
    protected AbstractProperty _hasFocusProperty;
    protected AbstractProperty _isMouseOverProperty;
    protected AbstractProperty _fontSizeProperty;
    protected AbstractProperty _fontFamilyProperty;

    protected AbstractProperty _contextMenuCommandProperty;

    protected PrimitiveBuffer _opacityMaskContext;
    protected bool _updateOpacityMask = false;
    protected RectangleF _lastOccupiedTransformedBounds = new RectangleF();
    protected Size _lastOpacityRenderSize = new Size();
    
    protected volatile SetFocusPriority _setFocusPrio = SetFocusPriority.None;

    protected SizeF? _availableSize;
    protected RectangleF? _outerRect;
    protected SizeF _innerDesiredSize; // Desiredd size in local coords
    protected SizeF _desiredSize; // Desired size in parent coordinate system
    protected RectangleF _innerRect;
    protected volatile bool _isMeasureInvalid = true;
    protected volatile bool _isArrangeInvalid = true;
    
    protected Matrix? _inverseFinalTransform = null;

    #endregion

    #region Ctor

    public FrameworkElement()
    {
      Init();
      Attach();
    }

    void Init()
    {
      // Default is not set
      _widthProperty = new SProperty(typeof(double), Double.NaN);
      _heightProperty = new SProperty(typeof(double), Double.NaN);

      // Default is not set
      _actualWidthProperty = new SProperty(typeof(double), Double.NaN);
      _actualHeightProperty = new SProperty(typeof(double), Double.NaN);

      // Min/Max width
      _minWidthProperty = new SProperty(typeof(double), 0.0);
      _minHeightProperty = new SProperty(typeof(double), 0.0);
      _maxWidthProperty = new SProperty(typeof(double), double.MaxValue);
      _maxHeightProperty = new SProperty(typeof(double), double.MaxValue);

      // Default is not set
      _styleProperty = new SProperty(typeof(Style), null);

      // Default is stretch
      _horizontalAlignmentProperty = new SProperty(typeof(HorizontalAlignmentEnum), HorizontalAlignmentEnum.Stretch);
      _verticalAlignmentProperty = new SProperty(typeof(VerticalAlignmentEnum), VerticalAlignmentEnum.Stretch);

      // Focus properties
      _focusableProperty = new SProperty(typeof(bool), false);
      _hasFocusProperty = new SProperty(typeof(bool), false);

      _isMouseOverProperty = new SProperty(typeof(bool), false);

      // Context menu
      _contextMenuCommandProperty = new SProperty(typeof(IExecutableCommand), null);

      // Font properties
      _fontFamilyProperty = new SProperty(typeof(string), String.Empty);
      _fontSizeProperty = new SProperty(typeof(int), -1);
    }

    void Attach()
    {
      _widthProperty.Attach(OnMeasureGetsInvalid);
      _heightProperty.Attach(OnMeasureGetsInvalid);
      _actualHeightProperty.Attach(OnActualBoundsChanged);
      _actualWidthProperty.Attach(OnActualBoundsChanged);
      _styleProperty.Attach(OnStyleChanged);

      _layoutTransformProperty.Attach(OnLayoutTransformPropertyChanged);
      _marginProperty.Attach(OnMeasureGetsInvalid);
      _visibilityProperty.Attach(OnParentCompleteLayoutGetsInvalid);
      _opacityProperty.Attach(OnOpacityChanged);
      _opacityMaskProperty.Attach(OnOpacityChanged);
      _actualPositionProperty.Attach(OnActualBoundsChanged);
    }

    void Detach()
    {
      _widthProperty.Detach(OnMeasureGetsInvalid);
      _heightProperty.Detach(OnMeasureGetsInvalid);
      _actualHeightProperty.Detach(OnActualBoundsChanged);
      _actualWidthProperty.Detach(OnActualBoundsChanged);
      _styleProperty.Detach(OnStyleChanged);

      _layoutTransformProperty.Detach(OnLayoutTransformPropertyChanged);
      _marginProperty.Detach(OnMeasureGetsInvalid);
      _visibilityProperty.Detach(OnParentCompleteLayoutGetsInvalid);
      _opacityProperty.Detach(OnOpacityChanged);
      _opacityMaskProperty.Detach(OnOpacityChanged);
      _actualPositionProperty.Detach(OnActualBoundsChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      object oldLayoutTransform = LayoutTransform;
      base.DeepCopy(source, copyManager);
      FrameworkElement fe = (FrameworkElement) source;
      Width = fe.Width;
      Height = fe.Height;
      Style = copyManager.GetCopy(fe.Style);
      ActualWidth = fe.ActualWidth;
      ActualHeight = fe.ActualHeight;
      HorizontalAlignment = fe.HorizontalAlignment;
      VerticalAlignment = fe.VerticalAlignment;
      Focusable = fe.Focusable;
      FontSize = fe.FontSize;
      FontFamily = fe.FontFamily;
      MinWidth = fe.MinWidth;
      MinHeight = fe.MinHeight;
      MaxWidth = fe.MaxWidth;
      MaxHeight = fe.MaxHeight;
      _setFocusPrio = fe.SetFocusPrio;

      // Need to manually call this because we are in a detached state
      OnLayoutTransformPropertyChanged(_layoutTransformProperty, oldLayoutTransform);

      Attach();
    }

    public override void Dispose()
    {
      Registration.TryCleanupAndDispose(ContextMenuCommand);
      Registration.TryCleanupAndDispose(Style);
      base.Dispose();
    }

    #endregion

    #region Event handlers

    protected virtual void OnStyleChanged(AbstractProperty property, object oldValue)
    {
      Style oldStyle = oldValue as Style;
      if (oldStyle != null)
      {
        oldStyle.Reset(this);
        Registration.TryCleanupAndDispose(oldStyle);
      }
      Style.Set(this);
      InvalidateLayout(true, true);
    }

    void OnActualBoundsChanged(AbstractProperty property, object oldValue)
    {
      _updateOpacityMask = true;
    }

    void OnLayoutTransformChanged(IObservable observable)
    {
      InvalidateLayout(true, true);
    }

    void OnLayoutTransformPropertyChanged(AbstractProperty property, object oldValue)
    {
      if (oldValue is Transform)
        ((Transform) oldValue).ObjectChanged -= OnLayoutTransformChanged;
      if (LayoutTransform != null)
        LayoutTransform.ObjectChanged += OnLayoutTransformChanged;
    }

    void OnOpacityChanged(AbstractProperty property, object oldValue)
    {
      _updateOpacityMask = true;
    }

    /// <summary>
    /// Called when a property has been changed which makes our arrangement invalid.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="oldValue">The old value of the property.</param>
    protected void OnArrangeGetsInvalid(AbstractProperty property, object oldValue)
    {
      InvalidateLayout(false, true);
    }

    /// <summary>
    /// Called when a property has been changed which makes our measurement invalid.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="oldValue">The old value of the property.</param>
    protected void OnMeasureGetsInvalid(AbstractProperty property, object oldValue)
    {
      InvalidateLayout(true, false);
    }

    /// <summary>
    /// Called when a property has been changed which makes our measurement and our arrangement invalid.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="oldValue">The old value of the property.</param>
    protected void OnCompleteLayoutGetsInvalid(AbstractProperty property, object oldValue)
    {
      InvalidateLayout(true, true);
    }

    /// <summary>
    /// Called when a property has been changed which makes our parent's measurement and its arrangement invalid.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="oldValue">The old value of the property.</param>
    protected void OnParentCompleteLayoutGetsInvalid(AbstractProperty property, object oldValue)
    {
      InvalidateParentLayout(true, true);
    }

    #endregion

    #region Public properties

    /// <summary>
    /// Returns the desired size this element calculated based on the available size.
    /// This value denotes the desired size of this element including its <see cref="UIElement.Margin"/> in the parent's coordinate
    /// system, i.e. with the <see cref="UIElement.RenderTransform"/> and <see cref="UIElement.LayoutTransform"/> applied.
    /// </summary>
    public SizeF DesiredSize
    {
      get { return _desiredSize; }
    }

    public AbstractProperty WidthProperty
    {
      get { return _widthProperty; }
    }

    public double Width
    {
      get { return (double) _widthProperty.GetValue(); }
      set { _widthProperty.SetValue(value); }
    }

    public AbstractProperty HeightProperty
    {
      get { return _heightProperty; }
    }

    public double Height
    {
      get { return (double) _heightProperty.GetValue(); }
      set { _heightProperty.SetValue(value); }
    }

    public AbstractProperty ActualWidthProperty
    {
      get { return _actualWidthProperty; }
    }

    public double ActualWidth
    {
      get { return (double) _actualWidthProperty.GetValue(); }
      set { _actualWidthProperty.SetValue(value); }
    }

    public AbstractProperty ActualHeightProperty
    {
      get { return _actualHeightProperty; }
    }

    public double ActualHeight
    {
      get { return (double) _actualHeightProperty.GetValue(); }
      set { _actualHeightProperty.SetValue(value); }
    }

    public AbstractProperty MinWidthProperty
    {
      get { return _minWidthProperty; }
    }

    public double MinWidth
    {
      get { return (double) _minWidthProperty.GetValue(); }
      set { _minWidthProperty.SetValue(value); }
    }

    public AbstractProperty MinHeightProperty
    {
      get { return _minHeightProperty; }
    }

    public double MinHeight
    {
      get { return (double) _minHeightProperty.GetValue(); }
      set { _minHeightProperty.SetValue(value); }
    }

    public AbstractProperty MaxWidthProperty
    {
      get { return _maxWidthProperty; }
    }

    public double MaxWidth
    {
      get { return (double) _maxWidthProperty.GetValue(); }
      set { _maxWidthProperty.SetValue(value); }
    }

    public AbstractProperty MaxHeightProperty
    {
      get { return _maxHeightProperty; }
    }

    public double MaxHeight
    {
      get { return (double) _maxHeightProperty.GetValue(); }
      set { _maxHeightProperty.SetValue(value); }
    }

    /// <summary>
    /// Gets this element's bounds in this element's coordinate system.
    /// This is a derived property which is calculated by the layout system.
    /// </summary>
    public RectangleF ActualBounds
    {
      get { return _innerRect; }
    }

    /// <summary>
    /// Gets the actual bounds plus <see cref="UIElement.Margin"/> plus the space which is needed for our
    /// <see cref="UIElement.LayoutTransform"/>.
    /// </summary>
    public RectangleF ActualTotalBounds
    {
      get { return _outerRect ?? new RectangleF(); }
    }

    public AbstractProperty HorizontalAlignmentProperty
    {
      get { return _horizontalAlignmentProperty; }
    }

    public HorizontalAlignmentEnum HorizontalAlignment
    {
      get { return (HorizontalAlignmentEnum) _horizontalAlignmentProperty.GetValue(); }
      set { _horizontalAlignmentProperty.SetValue(value); }
    }

    public AbstractProperty VerticalAlignmentProperty
    {
      get { return _verticalAlignmentProperty; }
    }

    public VerticalAlignmentEnum VerticalAlignment
    {
      get { return (VerticalAlignmentEnum) _verticalAlignmentProperty.GetValue(); }
      set { _verticalAlignmentProperty.SetValue(value); }
    }

    public AbstractProperty StyleProperty
    {
      get { return _styleProperty; }
    }

    public Style Style
    {
      get { return (Style) _styleProperty.GetValue(); }
      set { _styleProperty.SetValue(value); }
    }

    /// <summary>
    /// Helper property to make it possible in the screenfiles to set the focus to a framework element (or its first focusable child)
    /// when the screen is initialized. Use this property to set the initial focus.
    /// </summary>
    public SetFocusPriority SetFocusPrio
    {
      get { return _setFocusPrio; }
      set
      {
        _setFocusPrio = value;
        if (value > SetFocusPriority.None)
          InvalidateLayout(false, true);
      }
    }

    public bool SetFocus
    {
      get { return _setFocusPrio > SetFocusPriority.None; }
      set { _setFocusPrio = value ? SetFocusPriority.Default : SetFocusPriority.None; }
    }

    public AbstractProperty HasFocusProperty
    {
      get { return _hasFocusProperty; }
    }

    /// <summary>
    /// Returns the information whether this element currently has the focus. This element should not be set from the GUI!
    /// </summary>
    public virtual bool HasFocus
    {
      get { return (bool) _hasFocusProperty.GetValue(); }
      internal set { _hasFocusProperty.SetValue(value); }
    }

    public AbstractProperty FocusableProperty
    {
      get { return _focusableProperty; }
    }

    public bool Focusable
    {
      get { return (bool) _focusableProperty.GetValue(); }
      set { _focusableProperty.SetValue(value); }
    }

    public AbstractProperty IsMouseOverProperty
    {
      get { return _isMouseOverProperty; }
    }

    public bool IsMouseOver
    {
      get { return (bool) _isMouseOverProperty.GetValue(); }
      internal set { _isMouseOverProperty.SetValue(value); }
    }

    public AbstractProperty ContextMenuCommandProperty
    {
      get { return _contextMenuCommandProperty; }
    }

    public IExecutableCommand ContextMenuCommand
    {
      get { return (IExecutableCommand) _contextMenuCommandProperty.GetValue(); }
      internal set { _contextMenuCommandProperty.SetValue(value); }
    }

    public AbstractProperty FontFamilyProperty
    {
      get { return _fontFamilyProperty; }
    }

    // FontFamily & FontSize are defined in FrameworkElement because it should also be defined on
    // panels, and in MPF, panels inherit from FrameworkElement
    public string FontFamily
    {
      get { return (string) _fontFamilyProperty.GetValue(); }
      set { _fontFamilyProperty.SetValue(value); }
    }

    public AbstractProperty FontSizeProperty
    {
      get { return _fontSizeProperty; }
    }

    // FontFamily & FontSize are defined in FrameworkElement because it should also be defined on
    // panels, and in MPF, panels inherit from FrameworkElement
    public int FontSize
    {
      get { return (int) _fontSizeProperty.GetValue(); }
      set { _fontSizeProperty.SetValue(value); }
    }

    public bool IsMeasureInvalid
    {
      get { return _isMeasureInvalid; }
    }

    public bool IsArrangeInvalid
    {
      get { return _isArrangeInvalid; }
    }

    #endregion

    public string GetFontFamilyOrInherited()
    {
      string result = FontFamily;
      Visual current = this;
      while (string.IsNullOrEmpty(result) && (current = current.VisualParent) != null)
      {
        FrameworkElement currentFE = current as FrameworkElement;
        if (currentFE != null)
          result = currentFE.FontFamily;
      }
      return string.IsNullOrEmpty(result) ? FontManager.DefaultFontFamily : result;
    }

    public int GetFontSizeOrInherited()
    {
      int result = FontSize;
      Visual current = this;
      while (result == -1 && (current = current.VisualParent) != null)
      {
        FrameworkElement currentFE = current as FrameworkElement;
        if (currentFE != null)
          result = currentFE.FontSize;
      }
      return result == -1 ? FontManager.DefaultFontSize : result;
    }

    public override void OnKeyPreview(ref Key key)
    {
      base.OnKeyPreview(ref key);
      if (!HasFocus)
        return;
      if (key == Key.None) return;
      if (key == Key.ContextMenu && ContextMenuCommand != null)
      {
        ContextMenuCommand.Execute();
        key = Key.None;
      }
    }

    /// <summary>
    /// Checks if this element is focusable. This is the case if the element is visible, enabled and
    /// focusable. If this is the case, this method will set the focus to this element.
    /// </summary>
    public bool TrySetFocus(bool checkChildren)
    {
      if (HasFocus)
        return true;
      if (IsVisible && IsEnabled && Focusable)
      {
        Screen screen = Screen;
        if (screen == null)
          return false;
        RectangleF actualBounds = ActualBounds;
        MakeVisible(this, actualBounds);
        screen.UpdateFocusRect(actualBounds);
        screen.FrameworkElementGotFocus(this);
        HasFocus = true;
        return true;
      }
      if (checkChildren)
        return GetChildren().OfType<FrameworkElement>().Any(fe => fe.TrySetFocus(true));
      return false;
    }

    public void ResetFocus()
    {
      HasFocus = false;
      Screen screen = Screen;
      if (screen == null)
        return;
      screen.FrameworkElementLostFocus(this);
    }

    protected void UpdateFocus()
    {
      Screen screen = Screen;
      if (screen == null)
        return;
      if (_setFocusPrio <= screen.LastFocusPriority)
        // Only set the focus if our priority is higher than the last focus request in this layout cycle
        return;
      _setFocusPrio = SetFocusPriority.None;
      FrameworkElement fe = PredictFocus(null, MoveFocusDirection.Down);
      if (fe != null)
        fe.TrySetFocus(true);
    }

    /// <summary>
    /// Checks if the currently focused control is contained in this virtual keyboard control.
    /// </summary>
    public bool IsInFocusRootPath()
    {
      Screen screen = Screen;
      Visual focusPath = screen == null ? null : screen.FocusedElement;
      while (focusPath != null)
      {
        if (focusPath == this)
          // Focused control is located in our focus scope
          return true;
        focusPath = focusPath.VisualParent;
      }
      return false;
    }

    #region Replacing methods for the == operator which evaluate two float.NaN values to equal

    public static bool SameValue(float val1, float val2)
    {
      return float.IsNaN(val1) && float.IsNaN(val2) || val1 == val2;
    }

    public static bool SameSize(SizeF size1, SizeF size2)
    {
      return SameValue(size1.Width, size2.Width) && SameValue(size1.Height, size2.Height);
    }

    public static bool SameSize(SizeF? size1, SizeF size2)
    {
      return size1.HasValue && SameSize(size1.Value, size2);
    }

    public static bool SameRect(RectangleF rect1, RectangleF rect2)
    {
      return SameValue(rect1.X, rect2.X) && SameValue(rect1.Y, rect2.Y) && SameValue(rect1.Width, rect2.Width) && SameValue(rect1.Height, rect2.Height);
    }

    public static bool SameRect(RectangleF? rect1, RectangleF rect2)
    {
      return rect1.HasValue && SameRect(rect1.Value, rect2);
    }

    #endregion

    #region Measure & Arrange

    public void InvalidateLayout(bool invalidateMeasure, bool invalidateArrange)
    {
#if DEBUG_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("InvalidateLayout {0} Name='{1}', MeasureInvalid={2}, ArrangeInvalid={3}", GetType().Name, Name, invalidateMeasure, invalidateArrange));
#endif
      // Albert: Don't use this optimization because of threading issues
      //if (_isMeasureInvalid == invalidateMeasure && _isArrangeInvalid == invalidateArrange)
      //  return;
      _isMeasureInvalid |= invalidateMeasure;
      _isArrangeInvalid |= invalidateArrange;
      InvalidateParentLayout(invalidateMeasure, invalidateArrange);
    }

    /// <summary>
    /// Invalidates the layout of our visual parent.
    /// The parent will re-layout itself and its children.
    /// </summary>
    public void InvalidateParentLayout(bool invalidateMeasure, bool invalidateArrange)
    {
      FrameworkElement parent = VisualParent as FrameworkElement;
      if (parent != null)
        parent.InvalidateLayout(invalidateMeasure, invalidateArrange);
    }

    /// <summary>
    /// Given the transform currently applied to child, this method finds (in axis-aligned local space)
    /// the largest rectangle that, after transform, fits within <paramref name="localBounds"/>.
    /// Largest rectangle means rectangle of the greatest area in local space (although maximal area in local space
    /// implies maximal area in transform space).
    /// </summary>
    /// <param name="transform">Transformation matrix.</param>
    /// <param name="localBounds">The bounds in local space where the returned size fits when transformed
    /// via the given <paramref name="transform"/>.</param>
    /// <returns>The dimensions, in local space, of the maximal area rectangle found.</returns>
    private static SizeF FindMaxTransformedSize(Matrix transform, SizeF localBounds)
    {
      // X (width) and Y (height) constraints for axis-aligned bounding box in dest. space
      float xConstr = localBounds.Width;
      float yConstr = localBounds.Height;

      // Avoid doing math on an empty rect
      if (IsNear(xConstr, 0) || IsNear(yConstr, 0))
        return new SizeF(0, 0);

      bool xConstrInfinite = float.IsNaN(xConstr);
      bool yConstrInfinite = float.IsNaN(yConstr);

      if (xConstrInfinite && yConstrInfinite)
        return new SizeF(float.NaN, float.NaN);

      if (xConstrInfinite) // Assume square for one-dimensional constraint 
        xConstr = yConstr;
      else if (yConstrInfinite)
        yConstr = xConstr;

      // We only deal with nonsingular matrices here. The nonsingular matrix is the one
      // that has inverse (determinant != 0).
      if (transform.Determinant() == 0)
        return new SizeF(0, 0);

      float a = transform.M11;
      float b = transform.M12;
      float c = transform.M21;
      float d = transform.M22;

      // Result width and height (in child/local space)
      float w;
      float h;

      // Because we are dealing with nonsingular transform matrices, we have (b==0 || c==0) XOR (a==0 || d==0) 
      if (IsNear(b, 0) || IsNear(c, 0))
      { // (b == 0 || c == 0) ==> a != 0 && d != 0
        float yCoverD = yConstrInfinite ? float.PositiveInfinity : Math.Abs(yConstr / d);
        float xCoverA = xConstrInfinite ? float.PositiveInfinity : Math.Abs(xConstr / a);

        if (IsNear(b, 0))
        {
          if (IsNear(c, 0))
          { // b == 0, c == 0, a != 0, d != 0

            // No constraint relation; use maximal width and height 
            h = yCoverD;
            w = xCoverA;
          }
          else
          { // b == 0, a != 0, c != 0, d != 0

            // Maximizing under line (hIntercept=xConstr/c, wIntercept=xConstr/a) 
            // BUT we still have constraint: h <= yConstr/d
            h = Math.Min(0.5f * Math.Abs(xConstr / c), yCoverD);
            w = xCoverA - ((c * h) / a);
          }
        }
        else
        { // c == 0, a != 0, b != 0, d != 0 

          // Maximizing under line (hIntercept=yConstr/d, wIntercept=yConstr/b)
          // BUT we still have constraint: w <= xConstr/a
          w = Math.Min(0.5f * Math.Abs(yConstr / b), xCoverA);
          h = yCoverD - ((b * w) / d);
        }
      }
      else if (IsNear(a, 0) || IsNear(d, 0))
      { // (a == 0 || d == 0) ==> b != 0 && c != 0 
        float yCoverB = Math.Abs(yConstr / b);
        float xCoverC = Math.Abs(xConstr / c);

        if (IsNear(a, 0))
        {
          if (IsNear(d, 0))
          { // a == 0, d == 0, b != 0, c != 0 

            // No constraint relation; use maximal width and height
            h = xCoverC;
            w = yCoverB;
          }
          else
          { // a == 0, b != 0, c != 0, d != 0

            // Maximizing under line (hIntercept=yConstr/d, wIntercept=yConstr/b)
            // BUT we still have constraint: h <= xConstr/c
            h = Math.Min(0.5f * Math.Abs(yConstr / d), xCoverC);
            w = yCoverB - ((d * h) / b);
          }
        }
        else
        { // d == 0, a != 0, b != 0, c != 0

          // Maximizing under line (hIntercept=xConstr/c, wIntercept=xConstr/a)
          // BUT we still have constraint: w <= yConstr/b
          w = Math.Min(0.5f * Math.Abs(xConstr / a), yCoverB);
          h = xCoverC - ((a * w) / c);
        }
      }
      else
      {
        float xCoverA = Math.Abs(xConstr / a); // w-intercept of x-constraint line
        float xCoverC = Math.Abs(xConstr / c); // h-intercept of x-constraint line

        float yCoverB = Math.Abs(yConstr / b); // w-intercept of y-constraint line
        float yCoverD = Math.Abs(yConstr / d); // h-intercept of y-constraint line

        // The tighest constraint governs, so we pick the lowest constraint line

        // The optimal point (w, h) for which Area = w*h is maximized occurs halfway to each intercept.
        w = Math.Min(yCoverB, xCoverA) * 0.5f;
        h = Math.Min(xCoverC, yCoverD) * 0.5f;

        if ((GreaterThanOrClose(xCoverA, yCoverB) &&
             LessThanOrClose(xCoverC, yCoverD)) ||
            (LessThanOrClose(xCoverA, yCoverB) &&
             GreaterThanOrClose(xCoverC, yCoverD)))
        {
          // Constraint lines cross; since the most restrictive constraint wins,
          // we have to maximize under two line segments, which together are discontinuous.
          // Instead, we maximize w*h under the line segment from the two smallest endpoints. 

          // Since we are not (except for in corner cases) on the original constraint lines, 
          // we are not using up all the available area in transform space.  So scale our shape up 
          // until it does in at least one dimension.

          SizeF childSizeTr = new SizeF(w, h);
          transform.TransformIncludingRectangleSize(ref childSizeTr);
          float expandFactor = Math.Min(xConstr / childSizeTr.Width, yConstr / childSizeTr.Height);
          if (!float.IsNaN(expandFactor) && !float.IsInfinity(expandFactor))
          {
            w *= expandFactor;
            h *= expandFactor;
          }
        }
      }
      return new SizeF(w, h);
    }

    /// <summary>
    /// Measures this element's size and fills the <see cref="DesiredSize"/> property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is the first part of the two-phase measuring process. In this first phase, parent
    /// controls collect all the size requirements of their child controls.
    /// </para>
    /// <para>
    /// An input size value of <see cref="float.NaN"/> in any coordinate denotes that this child control doesn't have a size
    /// constraint in that direction. Coordinates different from <see cref="float.NaN"/> should be considered by this child
    /// control as the maximum available size in that direction. If this element still produces a bigger
    /// <see cref="DesiredSize"/>, the <see cref="Arrange(RectangleF)"/> method might give it a smaller final region.
    /// </para>
    /// </remarks>
    /// <param name="totalSize">Total size of the element including Margins. As input, this parameter
    /// contains the size available for this child control (size constraint). As output, it must be set
    /// to the <see cref="DesiredSize"/> plus <see cref="UIElement.Margin"/>.</param>
    public void Measure(ref SizeF totalSize)
    {
#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("Measure {0} Name='{1}', totalSize={2}", GetType().Name, Name, totalSize));
#endif
#endif
      if (!_isMeasureInvalid && SameSize(_availableSize, totalSize))
      { // Optimization: If our input data is the same and the layout isn't invalid, we don't need to measure again
        totalSize = _desiredSize;
#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
        System.Diagnostics.Trace.WriteLine(string.Format("Measure {0} Name='{1}', cutting short, totalSize is like before and measurement is not invalid, returns desired size={2}", GetType().Name, Name, totalSize));
#endif
#endif
        return;
      }
#if DEBUG_LAYOUT
#if !DEBUG_MORE_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("Measure {0} Name='{1}', totalSize={2}", GetType().Name, Name, totalSize));
#endif
#endif
      _isMeasureInvalid = false;
      _availableSize = new SizeF(totalSize);
      RemoveMargin(ref totalSize, Margin);

      Matrix? layoutTransform = LayoutTransform == null ? new Matrix?() : LayoutTransform.GetTransform();
      if (layoutTransform.HasValue)
        totalSize = FindMaxTransformedSize(layoutTransform.Value, totalSize);

      if (!double.IsNaN(Width))
        totalSize.Width = (float) Width;
      if (!double.IsNaN(Height))
        totalSize.Height = (float) Height;

      totalSize = CalculateInnerDesiredSize(new SizeF(totalSize));

      if (!double.IsNaN(Width))
        totalSize.Width = (float) Width;
      if (!double.IsNaN(Height))
        totalSize.Height = (float) Height;

      totalSize = ClampSize(totalSize);

      _innerDesiredSize = totalSize;

      if (layoutTransform.HasValue)
        layoutTransform.Value.TransformIncludingRectangleSize(ref totalSize);

      AddMargin(ref totalSize, Margin);
      if (totalSize != _desiredSize)
        InvalidateLayout(false, true);
      _desiredSize = totalSize;
#if DEBUG_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("Measure {0} Name='{1}', returns calculated desired size={2}", GetType().Name, Name, totalSize));
#endif
    }

    /// <summary>
    /// Arranges the UI element and positions it in the finalrect.
    /// </summary>
    /// <param name="outerRect">The final position and size the parent computed for this child element.</param>
    public void Arrange(RectangleF outerRect)
    {
      if (_isMeasureInvalid)
      {
#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
        System.Diagnostics.Trace.WriteLine(string.Format("Arrange {0} Name='{1}', exiting because measurement is invalid", GetType().Name, Name));
#endif
#endif
        return;
      }
#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("Arrange {0} Name='{1}', outerRect={2}", GetType().Name, Name, outerRect));
#endif
#endif
      if (!_isArrangeInvalid && SameRect(_outerRect, outerRect))
      { // Optimization: If our input data is the same and the layout isn't invalid, we don't need to arrange again
#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
        System.Diagnostics.Trace.WriteLine(string.Format("Arrange {0} Name='{1}', cutting short, outerRect={2} is like before and arrangement is not invalid", GetType().Name, Name, outerRect));
#endif
#endif
        return;
      }
#if DEBUG_LAYOUT
#if !DEBUG_MORE_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("Arrange {0} Name='{1}', outerRect={2}", GetType().Name, Name, outerRect));
#endif
#endif
      _isArrangeInvalid = false;
      _outerRect = new RectangleF(outerRect.Location, outerRect.Size);
      RectangleF rect = new RectangleF(outerRect.Location, outerRect.Size);
      RemoveMargin(ref rect, Margin);

      if (LayoutTransform != null)
      {
        Matrix layoutTransform = LayoutTransform.GetTransform().RemoveTranslation();
        if (!layoutTransform.IsIdentity)
        {
          SizeF resultInnerSize = _innerDesiredSize;
          SizeF resultOuterSize = new SizeF(resultInnerSize);
          layoutTransform.TransformIncludingRectangleSize(ref resultOuterSize);
          if (resultOuterSize.Width > rect.Width + DELTA_DOUBLE || resultOuterSize.Height > rect.Height + DELTA_DOUBLE)
            // Transformation of desired size doesn't fit into the available rect
            resultInnerSize = FindMaxTransformedSize(layoutTransform, rect.Size);
          rect = new RectangleF(
              rect.Location.X + (rect.Width - resultInnerSize.Width) / 2,
              rect.Location.Y + (rect.Height - resultInnerSize.Height) / 2,
              resultInnerSize.Width,
              resultInnerSize.Height);
        }
      }
      _innerRect = rect;

      InitializeTriggers();
      CheckFireLoaded(); // Has to be done after all triggers are initialized to make EventTriggers for UIElement.Loaded work properly

      ArrangeOverride();
      UpdateFocus(); // Has to be done after all children have arranged to make SetFocusPrio work properly
    }

    protected virtual void ArrangeOverride()
    {
      ActualPosition = _innerRect.Location;
      ActualWidth = _innerRect.Width;
      ActualHeight = _innerRect.Height;
    }

    protected virtual SizeF CalculateInnerDesiredSize(SizeF totalSize)
    {
      return SizeF.Empty;
    }

    protected SizeF ClampSize(SizeF size)
    {
      if (!float.IsNaN(size.Width))
        size.Width = (float) Math.Min(Math.Max(size.Width, MinWidth), MaxWidth);
      if (!float.IsNaN(size.Height))
        size.Height = (float) Math.Min(Math.Max(size.Height, MinHeight), MaxHeight);
      return size;
    }

    /// <summary>
    /// Arranges the child horizontal and vertical in a given area. If the area is bigger than
    /// the child's desired size, the child will be arranged according to the given <paramref name="horizontalAlignment"/>
    /// and <paramref name="verticalAlignment"/>.
    /// </summary>
    /// <param name="child">The child to arrange. The child will not be changed by this method.</param>
    /// <param name="horizontalAlignment">Alignment in horizontal direction.</param>
    /// <param name="verticalAlignment">Alignment in vertical direction.</param>
    /// <param name="location">Input: The starting position of the available area. Output: The position
    /// the child should be located.</param>
    /// <param name="childSize">Input: The available area for the <paramref name="child"/>. Output:
    /// The area the child should take.</param>
    public void ArrangeChild(FrameworkElement child, HorizontalAlignmentEnum horizontalAlignment, VerticalAlignmentEnum verticalAlignment, ref PointF location, ref SizeF childSize)
    {
      // Be careful when changing the implementation of those arrangement methods.
      // MPF behaves a bit different from WPF: We don't clip elements at the boundaries of containers,
      // instead, we arrange them with a maximum size calculated by the container. If we would not avoid
      // that controls can become bigger than their arrange size, we would have to accomplish a means to clip
      // their render size.
      ArrangeChildHorizontal(child, horizontalAlignment, ref location, ref childSize);
      ArrangeChildVertical(child, verticalAlignment, ref location, ref childSize);
    }

    /// <summary>
    /// Arranges the child horizontal in a given area. If the area is bigger than the child's desired
    /// size, the child will be arranged according to the given <paramref name="alignment"/>.
    /// </summary>
    /// <param name="child">The child to arrange. The child will not be changed by this method.</param>
    /// <param name="alignment">Alignment in horizontal direction.</param>
    /// <param name="location">Input: The starting position of the available area. Output: The position
    /// the child should be located.</param>
    /// <param name="childSize">Input: The available area for the <paramref name="child"/>. Output:
    /// The area the child should take.</param>
    public void ArrangeChildHorizontal(FrameworkElement child, HorizontalAlignmentEnum alignment, ref PointF location, ref SizeF childSize)
    {
      // See comment in ArrangeChild
      SizeF desiredSize = child.DesiredSize;

      if (!double.IsNaN(desiredSize.Width) && desiredSize.Width <= childSize.Width)
      {
        // Width takes precedence over Stretch - Use Center as fallback
        if (alignment == HorizontalAlignmentEnum.Center ||
            (alignment == HorizontalAlignmentEnum.Stretch && !double.IsNaN(child.Width)))
        {
          location.X += (childSize.Width - desiredSize.Width) / 2;
          childSize.Width = desiredSize.Width;
        }
        if (alignment == HorizontalAlignmentEnum.Right)
        {
          location.X += childSize.Width - desiredSize.Width;
          childSize.Width = desiredSize.Width;
        }
        else if (alignment == HorizontalAlignmentEnum.Left)
        {
          // Leave location unchanged
          childSize.Width = desiredSize.Width;
        }
        //else if (child.HorizontalAlignment == HorizontalAlignmentEnum.Stretch)
        // - Use all the space, nothing to do here
      }
    }

    /// <summary>
    /// Arranges the child vertical in a given area. If the area is bigger than the child's desired
    /// size, the child will be arranged according to the given <paramref name="alignment"/>.
    /// </summary>
    /// <param name="child">The child to arrange. The child will not be changed by this method.</param>
    /// <param name="alignment">Alignment in vertical direction.</param>
    /// <param name="location">Input: The starting position of the available area. Output: The position
    /// the child should be located.</param>
    /// <param name="childSize">Input: The available area for the <paramref name="child"/>. Output:
    /// The area the child should take.</param>
    public void ArrangeChildVertical(FrameworkElement child, VerticalAlignmentEnum alignment, ref PointF location, ref SizeF childSize)
    {
      // See comment in ArrangeChild
      SizeF desiredSize = child.DesiredSize;

      if (!double.IsNaN(desiredSize.Height) && desiredSize.Height <= childSize.Height)
      {
        // Height takes precedence over Stretch - Use Center as fallback
        if (alignment == VerticalAlignmentEnum.Center ||
            (alignment == VerticalAlignmentEnum.Stretch && !double.IsNaN(child.Height)))
        {
          location.Y += (childSize.Height - desiredSize.Height) / 2;
          childSize.Height = desiredSize.Height;
        }
        else if (alignment == VerticalAlignmentEnum.Bottom)
        {
          location.Y += childSize.Height - desiredSize.Height;
          childSize.Height = desiredSize.Height;
        }
        else if (alignment == VerticalAlignmentEnum.Top)
        {
          // Leave location unchanged
          childSize.Height = desiredSize.Height;
        }
        //else if (child.VerticalAlignment == VerticalAlignmentEnum.Stretch)
        // - Use all the space, nothing to do here
      }
    }

    #endregion

    /// <summary>
    /// Updates tle layout of this element in the render thread.
    /// In this method, <see cref="Measure(ref SizeF)"/> and <see cref="Arrange(RectangleF)"/> are called.
    /// </summary>
    /// <remarks>
    /// This method should actually be located in the <see cref="Screen"/> class but I leave it here because all the
    /// layout debug defines are in the scope of this file.
    /// This method must be called from the render thread before the call to <see cref="Render"/>.
    /// </remarks>
    public void UpdateLayoutRoot()
    {
      Screen screen = Screen;
      SizeF screenSize = screen == null ? SizeF.Empty : new SizeF(screen.SkinWidth, screen.SkinHeight);
      SizeF size = new SizeF(screenSize);

#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("UpdateLayoutRoot {0} Name='{1}', measuring with screen size {2}", GetType().Name, Name, screenSize));
#endif
#endif
      Measure(ref size);

#if DEBUG_LAYOUT
#if DEBUG_MORE_LAYOUT
      System.Diagnostics.Trace.WriteLine(string.Format("UpdateLayout {0} Name='{1}', arranging with screen size {2}", GetType().Name, Name, screenSize));
#endif
#endif
      // Ignore the measured size - arrange with screen size
      Arrange(new RectangleF(new PointF(0, 0), screenSize));
    }

    protected bool TransformMouseCoordinates(ref float x, ref float y)
    {
      Matrix? ift = _inverseFinalTransform;
      if (ift.HasValue)
      {
        ift.Value.Transform(ref x, ref y);
        return true;
      }
      return false;
    }

    public bool CanHandleMouseMove()
    {
      return _inverseFinalTransform.HasValue;
    }

    public override void OnMouseMove(float x, float y)
    {
      if (IsVisible)
      {
        bool hasFocus = HasFocus;
        float xTrans = x;
        float yTrans = y;
        if (!TransformMouseCoordinates(ref xTrans, ref yTrans))
          return;
        if (ActualBounds.Contains(xTrans, yTrans))
        {
          if (!IsMouseOver)
          {
            IsMouseOver = true;
            FireEvent(MOUSEENTER_EVENT, RoutingStrategyEnum.Direct);
          }
          bool inVisibleArea = IsInVisibleArea(xTrans, yTrans);
          if (!hasFocus && inVisibleArea)
            TrySetFocus(false);
          if (hasFocus && !inVisibleArea)
            ResetFocus();
        }
        else
        {
          if (IsMouseOver)
          {
            IsMouseOver = false;
            FireEvent(MOUSELEAVE_EVENT, RoutingStrategyEnum.Direct);
          }
          if (hasFocus)
            ResetFocus();
        }
      }
      base.OnMouseMove(x, y);
    }

    public override bool IsInArea(float x, float y)
    {
      return x >= ActualPosition.X && x <= ActualPosition.X + ActualWidth &&
          y >= ActualPosition.Y && y <= ActualPosition.Y + ActualHeight;
    }

    #region Focus & control predicition

    #region Focus movement

    protected FrameworkElement GetFocusedElementOrChild()
    {
      Screen screen = Screen;
      FrameworkElement result = screen == null ? null : screen.FocusedElement;
      if (result == null)
        foreach (UIElement child in GetChildren())
        {
          result = child as FrameworkElement;
          if (result != null)
            break;
        }
      return result;
    }

    /// <summary>
    /// Moves the focus from the currently focused element in the screen to the first child element in the given
    /// direction.
    /// </summary>
    /// <param name="direction">Direction to move the focus.</param>
    /// <returns><c>true</c>, if the focus could be moved to the desired child, else <c>false</c>.</returns>
    protected bool MoveFocus1(MoveFocusDirection direction)
    {
      FrameworkElement currentElement = GetFocusedElementOrChild();
      if (currentElement == null)
        return false;
      FrameworkElement nextElement = PredictFocus(currentElement.ActualBounds, direction);
      if (nextElement == null) return false;
      return nextElement.TrySetFocus(true);
    }

    /// <summary>
    /// Moves the focus from the currently focused element in the screen to our last child in the given
    /// direction. For example if <c>direction == MoveFocusDirection.Up</c>, this method tries to focus the
    /// topmost child.
    /// </summary>
    /// <param name="direction">Direction to move the focus.</param>
    /// <returns><c>true</c>, if the focus could be moved to the desired child, else <c>false</c>.</returns>
    protected bool MoveFocusN(MoveFocusDirection direction)
    {
      FrameworkElement currentElement = GetFocusedElementOrChild();
      if (currentElement == null)
        return false;
      ICollection<FrameworkElement> focusableChildren = GetFEChildren();
      if (focusableChildren.Count == 0)
        return false;
      FrameworkElement nextElement;
      while ((nextElement = FindNextFocusElement(focusableChildren, currentElement.ActualBounds, direction)) != null)
        currentElement = nextElement;
      return currentElement.TrySetFocus(true);
    }

    #endregion

    #region Focus prediction

    /// <summary>
    /// Predicts the next control located inside this element which is positioned in the specified direction
    /// <paramref name="dir"/> to the specified <paramref name="currentFocusRect"/> and
    /// which is able to get the focus.
    /// This method will search the control tree down starting with this element as root element.
    /// This method is only able to find focusable elements which are located at least one element outside the visible
    /// range (see <see cref="AddPotentialFocusableElements"/>).
    /// </summary>
    /// <param name="currentFocusRect">The borders of the currently focused control.</param>
    /// <param name="dir">Direction, the result control should be positioned relative to the
    /// currently focused control.</param>
    /// <returns>Framework element which should get the focus, or <c>null</c>, if this control
    /// tree doesn't contain an appropriate control. The returned control will be
    /// visible, focusable and enabled.</returns>
    public virtual FrameworkElement PredictFocus(RectangleF? currentFocusRect, MoveFocusDirection dir)
    {
      if (!IsVisible)
        return null;
      ICollection<FrameworkElement> focusableChildren = new List<FrameworkElement>();
      AddPotentialFocusableElements(currentFocusRect, focusableChildren);
      // Check child controls
      if (focusableChildren.Count == 0)
        return null;
      if (!currentFocusRect.HasValue)
        return focusableChildren.First();
      return FindNextFocusElement(focusableChildren, currentFocusRect, dir);
    }

    /// <summary>
    /// Searches through a collection of elements to find the best matching next focus element.
    /// </summary>
    /// <param name="potentialNextFocusElements">Collection of elements to search.</param>
    /// <param name="currentFocusRect">Bounds of the element which currently has focus.</param>
    /// <param name="dir">Direction to move the focus.</param>
    /// <returns>Next focusable element in the given <paramref name="dir"/> or <c>null</c>, if the given
    /// <paramref name="potentialNextFocusElements"/> don't contain a focusable element in the given direction.</returns>
    protected static FrameworkElement FindNextFocusElement(ICollection<FrameworkElement> potentialNextFocusElements,
        RectangleF? currentFocusRect, MoveFocusDirection dir)
    {
      FrameworkElement bestMatch = null;
      float bestDistance = float.MaxValue;
      float bestCenterDistance = float.MaxValue;
      if (!currentFocusRect.HasValue)
        return null;
      foreach (FrameworkElement child in potentialNextFocusElements)
      {
        if ((dir == MoveFocusDirection.Up && child.LocatedAbove(currentFocusRect.Value)) ||
            (dir == MoveFocusDirection.Down && child.LocatedBelow(currentFocusRect.Value)) ||
            (dir == MoveFocusDirection.Left && child.LocatedLeftOf(currentFocusRect.Value)) ||
            (dir == MoveFocusDirection.Right && child.LocatedRightOf(currentFocusRect.Value)))
        { // Calculate and compare distances of all matches
          float centerDistance = CenterDistance(child.ActualBounds, currentFocusRect.Value);
          if (centerDistance == 0)
            // If the child's center is exactly the center of the currently focused element,
            // it won't be used as next focus element
            continue;
          float distance = BorderDistance(child.ActualBounds, currentFocusRect.Value);
          if (bestMatch == null || distance < bestDistance ||
              distance == bestDistance && centerDistance < bestCenterDistance)
          {
            bestMatch = child;
            bestDistance = distance;
            bestCenterDistance = centerDistance;
          }
        }
      }
      return bestMatch;
    }

    protected static float BorderDistance(RectangleF r1, RectangleF r2)
    {
      float distX;
      float distY;
      if (r1.Left > r2.Right)
        distX = r1.Left - r2.Right;
      else if (r1.Right < r2.Left)
        distX = r2.Left - r1.Right;
      else
        distX = 0;
      if (r1.Top > r2.Bottom)
        distY = r1.Top - r2.Bottom;
      else if (r1.Bottom < r2.Top)
        distY = r2.Top - r1.Bottom;
      else
        distY = 0;
      return (float) Math.Sqrt(distX * distX + distY * distY);
    }

    protected static float CenterDistance(RectangleF r1, RectangleF r2)
    {
      float distX = Math.Abs((r1.Left + r1.Right) / 2 - (r2.Left + r2.Right) / 2);
      float distY = Math.Abs((r1.Top + r1.Bottom) / 2 - (r2.Top + r2.Bottom) / 2);
      return (float) Math.Sqrt(distX * distX + distY * distY);
    }

    protected PointF GetCenterPosition(RectangleF rect)
    {
      return new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }

    private static float CalcDirection(PointF start, PointF end)
    {
      if (IsNear(start.X, end.X) && IsNear(start.Y, end.Y))
        return float.NaN;
      double x = end.X - start.X;
      double y = end.Y - start.Y;
      double alpha = Math.Acos(x / Math.Sqrt(x * x + y * y));
      if (end.Y > start.Y) // Coordinates go from top to bottom, so y must be inverted
        alpha = -alpha;
      if (alpha < 0)
        alpha += 2 * Math.PI;
      return (float) alpha;
    }

    protected static bool AInsideB(RectangleF a, RectangleF b)
    {
      return b.Left <= a.Left && b.Right >= a.Right &&
          b.Top <= a.Top && b.Bottom >= a.Bottom;
    }

    protected bool LocatedInside(RectangleF otherRect)
    {
      return AInsideB(ActualBounds, otherRect);
    }

    protected bool EnclosesRect(RectangleF otherRect)
    {
      return AInsideB(otherRect, ActualBounds);
    }

    protected bool LocatedBelow(RectangleF otherRect)
    {
      RectangleF actualBounds = ActualBounds;
      if (IsNear(actualBounds.Top, otherRect.Bottom))
        return true;
      PointF start = new PointF((actualBounds.Right + actualBounds.Left) / 2, actualBounds.Top);
      PointF end = new PointF((otherRect.Right + otherRect.Left) / 2, otherRect.Bottom);
      float alpha = CalcDirection(start, end);
      return alpha > DELTA_DOUBLE && alpha < Math.PI - DELTA_DOUBLE;
    }

    protected bool LocatedAbove(RectangleF otherRect)
    {
      RectangleF actualBounds = ActualBounds;
      if (IsNear(actualBounds.Bottom, otherRect.Top))
        return true;
      PointF start = new PointF((actualBounds.Right + actualBounds.Left) / 2, actualBounds.Bottom);
      PointF end = new PointF((otherRect.Right + otherRect.Left) / 2, otherRect.Top);
      float alpha = CalcDirection(start, end);
      return alpha > Math.PI + DELTA_DOUBLE && alpha < 2 * Math.PI - DELTA_DOUBLE;
    }

    protected bool LocatedLeftOf(RectangleF otherRect)
    {
      RectangleF actualBounds = ActualBounds;
      if (IsNear(actualBounds.Right, otherRect.Left))
        return true;
      PointF start = new PointF(actualBounds.Right, (actualBounds.Top + actualBounds.Bottom) / 2);
      PointF end = new PointF(otherRect.Left, (otherRect.Top + otherRect.Bottom) / 2);
      float alpha = CalcDirection(start, end);
      return alpha < Math.PI / 2 - DELTA_DOUBLE || alpha > 3 * Math.PI / 2 + DELTA_DOUBLE;
    }

    protected bool LocatedRightOf(RectangleF otherRect)
    {
      RectangleF actualBounds = ActualBounds;
      if (IsNear(actualBounds.Left, otherRect.Right))
        return true;
      PointF start = new PointF(actualBounds.Left, (actualBounds.Top + actualBounds.Bottom) / 2);
      PointF end = new PointF(otherRect.Right, (otherRect.Top + otherRect.Bottom) / 2);
      float alpha = CalcDirection(start, end);
      return alpha > Math.PI / 2 + DELTA_DOUBLE && alpha < 3 * Math.PI / 2 - DELTA_DOUBLE;
    }

    /// <summary>
    /// Collects all focusable elements in the element tree starting with this element which are potentially located next
    /// to the given <paramref name="startingRect"/>.
    /// </summary>
    /// <remarks>
    /// This default implementation simply returns this element and all children, but sub classes might restrict the
    /// result collection.
    /// The less elements are returned, the faster the focusing engine can find an element to be focused.
    /// </remarks>
    /// <param name="startingRect">Rectangle where to start searching. If this parameter is <c>null</c> (i.e. has no value),
    /// all potential focusable elements should be returned.</param>
    /// <param name="elements">Collection to add elements which are able to get the focus.</param>
    public virtual void AddPotentialFocusableElements(RectangleF? startingRect, ICollection<FrameworkElement> elements)
    {
      if (!IsVisible)
        return;
      if (Focusable)
        elements.Add(this);
      // General implementation: Return all visible children
      ICollection<FrameworkElement> children = GetFEChildren();
      foreach (FrameworkElement child in children)
      {
        if (!child.IsVisible)
          continue;
        child.AddPotentialFocusableElements(startingRect, elements);
      }
    }

    protected ICollection<FrameworkElement> GetFEChildren()
    {
      ICollection<UIElement> children = GetChildren();
      ICollection<FrameworkElement> result = new List<FrameworkElement>(children.Count);
      foreach (UIElement child in children)
      {
        FrameworkElement fe = child as FrameworkElement;
        if (fe != null)
          result.Add(fe);
      }
      return result;
    }

    #endregion

    #endregion

    public override void SaveUIState(IDictionary<string, object> state, string prefix)
    {
      base.SaveUIState(state, prefix);
      if (HasFocus)
        state[prefix + "/Focused"] = true;
    }

    public override void RestoreUIState(IDictionary<string, object> state, string prefix)
    {
      base.RestoreUIState(state, prefix);
      object focused;
      bool? bFocused;
      if (state.TryGetValue(prefix + "/Focused", out focused) && (bFocused = focused as bool?).HasValue && bFocused.Value)
        SetFocusPrio = SetFocusPriority.RestoreState;
    }

    public virtual void DoRender(RenderContext localRenderContext)
    {
    }

    public void RenderToTexture(RenderTextureAsset texture, RenderContext renderContext)
    {
      // We do the following here:
      // 1. Set the transformation matrix to match the texture size
      // 2. Set the rendertarget to the given texture
      // 3. Clear the texture with an alpha value of 0
      // 4. Render the control (into the texture)
      // 5. Restore the rendertarget to the backbuffer
      // 6. Restore previous transformation matrix

      // Set transformation matrix
      Matrix? oldTransform = null;
      if (texture.Width != GraphicsDevice.Width || texture.Height != GraphicsDevice.Height)
      {
        oldTransform = GraphicsDevice.FinalTransform;
        GraphicsDevice.SetCameraProjection(texture.Width, texture.Height);
      }
      // Get the current backbuffer
      using (Surface backBuffer = GraphicsDevice.Device.GetRenderTarget(0))
      {
        // Change the rendertarget to the render texture
        GraphicsDevice.Device.SetRenderTarget(0, texture.Surface0);

        // Fill the background of the texture with an alpha value of 0
        GraphicsDevice.Device.Clear(ClearFlags.Target, Color.FromArgb(0, Color.Black), 1.0f, 0);

        // Render the control into the given texture
        DoRender(renderContext);

        // Restore the backbuffer
        GraphicsDevice.Device.SetRenderTarget(0, backBuffer);
      }
      // Restore standard transformation matrix
      if (oldTransform.HasValue)
        GraphicsDevice.FinalTransform = oldTransform.Value;
    }

    public override void Render(RenderContext parentRenderContext)
    {
      if (!IsVisible)
        return;

      RectangleF bounds = ActualBounds;
      if (bounds.Width <= 0 || bounds.Height <= 0)
        return;

      Matrix? layoutTransformMatrix = LayoutTransform == null ? new Matrix?() : LayoutTransform.GetTransform();
      Matrix? renderTransformMatrix = RenderTransform == null ? new Matrix?() : RenderTransform.GetTransform();

      RenderContext localRenderContext = parentRenderContext.Derive(bounds, layoutTransformMatrix,
          renderTransformMatrix, RenderTransformOrigin, Opacity);
      _inverseFinalTransform = Matrix.Invert(localRenderContext.MouseTransform);

      if (OpacityMask == null)
        // Simply render without opacity mask
        DoRender(localRenderContext);
      else
      { // Control has an opacity mask
        // Get global render texture or create it if it doesn't exist
        RenderTextureAsset renderTarget = ServiceRegistration.Get<ContentManager>().GetRenderTexture(
            GLOBAL_RENDER_TEXTURE_ASSET_KEY);

        // Ensure it's allocated
        renderTarget.AllocateRenderTarget(GraphicsDevice.Width, GraphicsDevice.Height);
        if (!renderTarget.IsAllocated)
          return;

        // Create a temporary render context and render the control to the render texture
        RenderContext tempRenderContext = new RenderContext(localRenderContext.Transform, Matrix.Identity, 
          localRenderContext.Opacity, bounds, localRenderContext.ZOrder);
        RenderToTexture(renderTarget, tempRenderContext);

        // Add bounds to our calculated, occupied area.
        // If we don't do that, lines at the border of this element might be dimmed because of the filter (see OpacityMask test in GUITestPlugin).
        // The value was just found by testing. Any better solution is welcome.
        const float OPACITY_MASK_BOUNDS = 0.9f;
        RectangleF occupiedTransformedBounds = tempRenderContext.OccupiedTransformedBounds;
        occupiedTransformedBounds.X -= OPACITY_MASK_BOUNDS;
        occupiedTransformedBounds.Y -= OPACITY_MASK_BOUNDS;
        occupiedTransformedBounds.Width += OPACITY_MASK_BOUNDS*2;
        occupiedTransformedBounds.Height += OPACITY_MASK_BOUNDS*2;

        // If the control bounds have changed we need to update our primitive context to make the 
        //    texture coordinates match up
        if (_updateOpacityMask || _opacityMaskContext == null ||
            occupiedTransformedBounds != _lastOccupiedTransformedBounds ||
            renderTarget.Size != _lastOpacityRenderSize)
        {
          UpdateOpacityMask(occupiedTransformedBounds, renderTarget.Width, renderTarget.Height, localRenderContext.ZOrder);
          _lastOccupiedTransformedBounds = occupiedTransformedBounds;
          _updateOpacityMask = false;
          _lastOpacityRenderSize = renderTarget.Size;
        }

        // Now render the opacitytexture with the OpacityMask brush
        OpacityMask.BeginRenderOpacityBrush(renderTarget.Texture, new RenderContext(Matrix.Identity, Matrix.Identity, bounds));
        _opacityMaskContext.Render(0);
        OpacityMask.EndRender();
      }
      // Calculation of absolute render size (in world coordinate system)
      parentRenderContext.IncludeTransformedContentsBounds(localRenderContext.OccupiedTransformedBounds);
    }

    #region Opacitymask

    void UpdateOpacityMask(RectangleF bounds, float width, float height, float zPos)
    {
      Color4 col = ColorConverter.FromColor(Color.White);
      col.Alpha *= (float) Opacity;
      int color = col.ToArgb();

      PositionColoredTextured[] verts = PositionColoredTextured.CreateQuad_Fan(
          bounds.Left - 0.5f, bounds.Top - 0.5f, bounds.Right - 0.5f, bounds.Bottom - 0.5f,
          bounds.Left / width, bounds.Top / height, bounds.Right / width, bounds.Bottom / height,
          zPos, color);

      OpacityMask.SetupBrush(this, ref verts, zPos, false);
      PrimitiveBuffer.SetPrimitiveBuffer(ref _opacityMaskContext, ref verts, PrimitiveType.TriangleFan);
    }

    #endregion

    public override void Deallocate()
    {
      base.Deallocate();
      PrimitiveBuffer.DisposePrimitiveBuffer(ref _opacityMaskContext);
    }
  }
}
