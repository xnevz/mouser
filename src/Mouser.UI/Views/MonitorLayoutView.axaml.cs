using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mouser.Core.Geometry;
using Mouser.Core.Kvm;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class MonitorLayoutView : UserControl
{
    private Visual? _canvas;
    private Control? _draggedControl;
    private ScreenNodeViewModel? _draggedNode;
    private Point _startPointerPos;
    private double _initialNodeX;
    private double _initialNodeY;
    private bool _isDragging;
    private bool _hasDragged;

    public MonitorLayoutView()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, OnScreenPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnScreenPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnScreenPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerCaptureLostEvent, OnScreenPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private void OnScreenPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MonitorLayoutViewModel vm) return;

        var button = (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true);
        if (button?.DataContext is not ScreenNodeViewModel node || !node.IsPeer || node.Peer == null)
            return;

        var pointerProps = e.GetCurrentPoint(this).Properties;
        if (!pointerProps.IsLeftButtonPressed) return;

        _draggedNode = node;
        _draggedControl = button;
        vm.SelectScreenCommand.Execute(node);

        var canvas = button.FindAncestorOfType<Canvas>()
                     ?? (Visual?)this.FindControl<ItemsControl>("ScreensItemsControl")?.FindDescendantOfType<Canvas>()
                     ?? (Visual?)this.FindControl<ItemsControl>("ScreensItemsControl")
                     ?? this;

        _canvas = canvas;
        _startPointerPos = e.GetPosition(canvas);
        _initialNodeX = node.PositionX;
        _initialNodeY = node.PositionY;
        _hasDragged = false;
        _isDragging = true;

        e.Pointer.Capture(button);
        e.Handled = true;
    }

    private void OnScreenPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _draggedNode == null || DataContext is not MonitorLayoutViewModel) return;

        var canvas = _canvas
                     ?? (sender as Visual)?.FindAncestorOfType<Canvas>()
                     ?? (Visual?)this.FindControl<ItemsControl>("ScreensItemsControl")?.FindDescendantOfType<Canvas>()
                     ?? this;

        var currentPos = e.GetPosition(canvas);
        double deltaX = currentPos.X - _startPointerPos.X;
        double deltaY = currentPos.Y - _startPointerPos.Y;

        if (Math.Abs(deltaX) > 2 || Math.Abs(deltaY) > 2)
        {
            _hasDragged = true;
        }

        double newX = _initialNodeX + deltaX;
        double newY = _initialNodeY + deltaY;

        _draggedNode.PositionX = newX;
        _draggedNode.PositionY = newY;

        if (_draggedControl != null)
        {
            var presenter = _draggedControl.FindAncestorOfType<ContentPresenter>();
            if (presenter != null)
            {
                Canvas.SetLeft(presenter, newX);
                Canvas.SetTop(presenter, newY);
            }
        }

        e.Handled = true;
    }

    private void OnScreenPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging || _draggedNode == null) return;

        var pointer = e.Pointer;
        var node = _draggedNode;
        bool dragged = _hasDragged;

        pointer.Capture(null);
        _isDragging = false;
        _hasDragged = false;
        _draggedNode = null;
        _draggedControl = null;
        _canvas = null;

        if (dragged && DataContext is MonitorLayoutViewModel vm && node.Peer is { } peer && vm.LocalDesktop is { } local)
        {
            ApplySnappingAndCommit(vm, node, peer, local);
        }

        e.Handled = true;
    }

    private void OnScreenPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDragging = false;
        _hasDragged = false;
        _draggedNode = null;
        _draggedControl = null;
        _canvas = null;
    }

    internal static void ApplySnappingAndCommit(MonitorLayoutViewModel vm, ScreenNodeViewModel node, PeerViewModel peer, ScreenBounds local)
    {
        double remoteW = peer.Peer.ScreenWidth > 0 ? peer.Peer.ScreenWidth : 1920;
        double remoteH = peer.Peer.ScreenHeight > 0 ? peer.Peer.ScreenHeight : 1080;
        double scale = vm.CanvasScale > 0 ? vm.CanvasScale : 1.0;

        double peerCanvasW = remoteW * scale;
        double peerCanvasH = remoteH * scale;

        double localCanvasX = vm.CanvasOriginX + (local.X - vm.CanvasMinX) * scale;
        double localCanvasY = vm.CanvasOriginY + (local.Y - vm.CanvasMinY) * scale;
        double localCanvasW = local.Width * scale;
        double localCanvasH = local.Height * scale;
        double localCanvasRight = localCanvasX + localCanvasW;
        double localCanvasBottom = localCanvasY + localCanvasH;

        double peerLeft = node.PositionX;
        double peerTop = node.PositionY;
        double peerRight = peerLeft + peerCanvasW;
        double peerBottom = peerTop + peerCanvasH;

        // Determine closest side of the local desktop (Left, Right, Above, Below)
        double distRight = Math.Abs(peerLeft - localCanvasRight);
        double distLeft = Math.Abs(peerRight - localCanvasX);
        double distBelow = Math.Abs(peerTop - localCanvasBottom);
        double distAbove = Math.Abs(peerBottom - localCanvasY);

        double minDist = Math.Min(Math.Min(distRight, distLeft), Math.Min(distBelow, distAbove));

        PeerPlacement placement;
        if (minDist == distRight) placement = PeerPlacement.Right;
        else if (minDist == distLeft) placement = PeerPlacement.Left;
        else if (minDist == distBelow) placement = PeerPlacement.Below;
        else placement = PeerPlacement.Above;

        // Snapping logic:
        // If near edge is within 24 px (canvas) of local desktop edge, snap to it.
        // Snap offset (along the edge) to 0 when within 12 px (canvas) of aligned top/left, or center.
        int offsetX = 0;
        int offsetY = 0;

        if (placement is PeerPlacement.Left or PeerPlacement.Right)
        {
            double topDiff = Math.Abs(peerTop - localCanvasY);
            double localCenterY = localCanvasY + localCanvasH / 2.0;
            double peerCenterY = peerTop + peerCanvasH / 2.0;
            double centerDiffY = Math.Abs(peerCenterY - localCenterY);

            if (topDiff <= 12.0)
            {
                offsetY = 0; // snapped to top
            }
            else if (centerDiffY <= 12.0)
            {
                offsetY = (int)Math.Round((local.Height - remoteH) / 2.0); // snapped to center
            }
            else
            {
                offsetY = (int)Math.Round((peerTop - localCanvasY) / scale);
            }
        }
        else // Above or Below
        {
            double leftDiff = Math.Abs(peerLeft - localCanvasX);
            double localCenterX = localCanvasX + localCanvasW / 2.0;
            double peerCenterX = peerLeft + peerCanvasW / 2.0;
            double centerDiffX = Math.Abs(peerCenterX - localCenterX);

            if (leftDiff <= 12.0)
            {
                offsetX = 0; // snapped to left
            }
            else if (centerDiffX <= 12.0)
            {
                offsetX = (int)Math.Round((local.Width - remoteW) / 2.0); // snapped to center
            }
            else
            {
                offsetX = (int)Math.Round((peerLeft - localCanvasX) / scale);
            }
        }

        vm.SetPeerPlacementAndOffset(peer.Id, placement, offsetX, offsetY);
    }
}
