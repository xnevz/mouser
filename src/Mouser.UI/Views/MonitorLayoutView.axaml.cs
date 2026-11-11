using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class MonitorLayoutView : UserControl
{
    private Visual? _canvas;
    private Control? _draggedControl;
    private ScreenNodeViewModel? _draggedScreen;
    private Point _pointerStartPosOnCanvas;
    private double _initialScreenX;
    private double _initialScreenY;
    private bool _isDragging;

    public MonitorLayoutView()
    {
        InitializeComponent();
    }

    private void OnScreenPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is ScreenNodeViewModel screen && DataContext is MonitorLayoutViewModel vm)
        {
            var pointerProps = e.GetCurrentPoint(control).Properties;
            if (pointerProps.IsLeftButtonPressed)
            {
                _draggedScreen = screen;
                _draggedControl = control;
                vm.SelectScreenCommand.Execute(screen);

                // Find the hosting Canvas or items container dynamically from the visual tree
                var canvas = control.FindAncestorOfType<Canvas>()
                             ?? (Visual?)this.FindControl<ItemsControl>("ScreensItemsControl")?.FindDescendantOfType<Canvas>()
                             ?? (Visual?)this.FindControl<ItemsControl>("ScreensItemsControl")
                             ?? this;

                _canvas = canvas;
                _pointerStartPosOnCanvas = e.GetPosition(canvas);
                _initialScreenX = screen.PositionX;
                _initialScreenY = screen.PositionY;
                _isDragging = true;
                screen.IsDragging = true;
                vm.IsDraggingAnyScreen = true;
                e.Pointer.Capture(control);
                e.Handled = true;
            }
        }
    }

    private void OnScreenPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && _draggedScreen != null && DataContext is MonitorLayoutViewModel vm)
        {
            var canvas = _canvas
                         ?? (sender as Visual)?.FindAncestorOfType<Canvas>()
                         ?? (Visual?)this.FindControl<ItemsControl>("ScreensItemsControl")?.FindDescendantOfType<Canvas>()
                         ?? this;

            var currentPos = e.GetPosition(canvas);
            double deltaX = currentPos.X - _pointerStartPosOnCanvas.X;
            double deltaY = currentPos.Y - _pointerStartPosOnCanvas.Y;

            double targetX = _initialScreenX + deltaX;
            double targetY = _initialScreenY + deltaY;

            vm.UpdateScreenPosition(_draggedScreen, targetX, targetY, enableSnapping: true);

            // Directly position the ContentPresenter on Canvas so visual feedback is instant
            if (_draggedControl != null)
            {
                var presenter = _draggedControl.FindAncestorOfType<ContentPresenter>();
                if (presenter != null)
                {
                    Canvas.SetLeft(presenter, _draggedScreen.PositionX);
                    Canvas.SetTop(presenter, _draggedScreen.PositionY);
                }
            }

            e.Handled = true;
        }
    }

    private void OnScreenPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag(e.Pointer);
        e.Handled = true;
    }

    private void OnScreenPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndDrag(null);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (_isDragging)
        {
            _isDragging = false;
            if (_draggedScreen != null)
            {
                _draggedScreen.IsDragging = false;
            }
            if (_draggedControl != null && _draggedScreen != null)
            {
                var presenter = _draggedControl.FindAncestorOfType<ContentPresenter>();
                if (presenter != null)
                {
                    Canvas.SetLeft(presenter, _draggedScreen.PositionX);
                    Canvas.SetTop(presenter, _draggedScreen.PositionY);
                }
            }
            if (DataContext is MonitorLayoutViewModel vm)
            {
                vm.IsDraggingAnyScreen = false;
            }
            pointer?.Capture(null);
            _draggedControl = null;
            _draggedScreen = null;
            _canvas = null;
        }
    }
}
