using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class MonitorLayoutView : UserControl
{
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
                vm.SelectScreenCommand.Execute(screen);

                var canvas = this.FindControl<Canvas>("ScreenCanvas");
                if (canvas != null)
                {
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
    }

    private void OnScreenPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && _draggedScreen != null && DataContext is MonitorLayoutViewModel vm)
        {
            var canvas = this.FindControl<Canvas>("ScreenCanvas");
            if (canvas != null)
            {
                var currentPos = e.GetPosition(canvas);
                double deltaX = currentPos.X - _pointerStartPosOnCanvas.X;
                double deltaY = currentPos.Y - _pointerStartPosOnCanvas.Y;

                double targetX = _initialScreenX + deltaX;
                double targetY = _initialScreenY + deltaY;

                vm.UpdateScreenPosition(_draggedScreen, targetX, targetY, enableSnapping: true);
                e.Handled = true;
            }
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
            if (DataContext is MonitorLayoutViewModel vm)
            {
                vm.IsDraggingAnyScreen = false;
            }
            pointer?.Capture(null);
            _draggedScreen = null;
        }
    }
}
