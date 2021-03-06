﻿//
// Author:
//   Larry Ewing <lewing@xamarin.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using Xamarin.Interactive.Client.ViewInspector;
using Xamarin.Interactive.Client.Windows.Views;

namespace Xamarin.Interactive.Client.Windows.ViewModels
{

    class InspectTreeNode3D : ModelVisual3D, IInspectTree3DNode<InspectTreeNode3D>
    {
        static readonly double ZSpacing = .1;
        static readonly double zFightIncrement = 1 / 800.0;

        static Color FocusColor => Color.FromArgb (200, 0x1, 0x73, 0xc7);
        static Color SecondaryFocusColor => Color.FromArgb (200, 0x2a, 0x8a, 0xd4);
        static Color BlurColor => Color.FromArgb (200, 255, 255, 255);
        static Color EmptyColor => Color.FromArgb (1, 255, 255, 255);
        static Color HoverColor => Color.FromArgb (128, 0xe6, 0xf2, 0xfa);

        readonly ScaleTransform3D expandTransform = new ScaleTransform3D ();
        readonly int childIndex;
        DiffuseMaterial material;

        public InspectTreeNode Node { get; }

        public InspectTreeNode3D (InspectTreeNode node, InspectTreeState state)
        {
            void NodePropertyChanged (object sender, PropertyChangedEventArgs args)
            {
                var senderNode = sender as InspectTreeNode;
                switch (args.PropertyName) {
                case nameof (InspectTreeNode.Children):
                    break;
                case nameof (InspectTreeNode.IsSelected):
                case nameof (InspectTreeNode.IsMouseOver):
                    UpdateMaterial ();
                    break;
                case nameof (InspectTreeNode.IsExpanded):
                    foreach (var child in Children.OfType<InspectTreeNode3D> ())
                        child.IsFlattened = !senderNode.IsExpanded;
                    break;
                }
            }
            Node = node;
            node.PropertyChanged += NodePropertyChanged;
            childIndex = state.AddChild (node.View);
        }

        void BuildPrimaryPlane (InspectTreeState state)
        {
            var displayMode = state.Mode;
            Brush brush = new SolidColorBrush (EmptyColor);
            var view = Node.View;
            var parent = Node.View.Parent;
            var matrix = Matrix3D.Identity;

            if (view.Layer != null)
                view = view.Layer;

            var zFightOffset = childIndex * zFightIncrement;
            var zOffset = ZSpacing + zFightOffset;

            if (view.Transform != null) {
                var render = view.Transform;
                matrix = new Matrix3D {
                    M11 = render.M11,
                    M12 = render.M12,
                    M13 = render.M13,
                    M14 = render.M14,
                    M21 = render.M21,
                    M22 = render.M22,
                    M23 = render.M23,
                    M24 = render.M24,
                    M31 = render.M31,
                    M32 = render.M32,
                    M33 = render.M33,
                    M34 = render.M34,
                    OffsetX = render.OffsetX,
                    OffsetY = render.OffsetY,
                    OffsetZ = render.OffsetZ + zOffset
                };
            }

            var size = new Size (view.Width, view.Height);
            var visual = new DrawingVisual ();
            using (var context = visual.RenderOpen ()) {

                if (view.BestCapturedImage != null && displayMode.HasFlag (DisplayMode.Content)) {
                    var bitmap = new BitmapImage ();
                    bitmap.BeginInit ();
                    bitmap.StreamSource = new MemoryStream (view.BestCapturedImage);
                    bitmap.EndInit ();

                    context.DrawImage (bitmap, new Rect (size));
                }

                if (displayMode.HasFlag (DisplayMode.Frames))
                    context.DrawRectangle (
                        null,
                        new Pen (new SolidColorBrush (Color.FromRgb (0xd3, 0xd3, 0xd3)), 0.5),
                        new Rect (size));
            }

            brush = new ImageBrush { ImageSource = new DrawingImage (visual.Drawing) };

            var geometry = new MeshGeometry3D () {
                Positions = new Point3DCollection {
                    new Point3D (0, 0, 0),
                    new Point3D (0, -size.Height, 0),
                    new Point3D (size.Width, -size.Height, 0),
                    new Point3D (size.Width, 0, 0)
                },
                TextureCoordinates = new PointCollection {
                    new Point (0,0),
                    new Point (0,1),
                    new Point (1,1),
                    new Point (1,0)
                },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 },
            };

            var backGeometry = new MeshGeometry3D () {
                Positions = geometry.Positions,
                TextureCoordinates = geometry.TextureCoordinates,
                TriangleIndices = geometry.TriangleIndices,
                Normals = new Vector3DCollection {
                    new Vector3D (0, 0, -1),
                    new Vector3D (0, 0, -1),
                    new Vector3D (0, 0, -1),
                    new Vector3D (0, 0, -1)
                }
            };

            material = new DiffuseMaterial (brush) { Color = BlurColor };

            Content = new Model3DGroup () {
                Children = new Model3DCollection {
                    new GeometryModel3D {
                        Geometry = geometry,
                        Material = material
                    },
                    new GeometryModel3D {
                        Geometry = backGeometry,
                        BackMaterial = material,
                    },
                },
                Transform = new ScaleTransform3D {
                    ScaleX = Math.Ceiling (view.Width) / size.Width,
                    ScaleY = -Math.Ceiling (view.Height) / size.Height,
                    ScaleZ = 1
                }
            };

            var group = new Transform3DGroup ();
            if ((parent == null && !Node.View.IsFakeRoot) || (parent?.IsFakeRoot ?? false)) {
                var unitScale = 1.0 / Math.Max (view.Width, view.Height);
                group.Children = new Transform3DCollection {
                        new TranslateTransform3D {
                            OffsetX = -view.Width / 2.0,
                            OffsetY = -view.Height / 2.0,
                            OffsetZ = zOffset
                        },
                        new ScaleTransform3D (unitScale, -unitScale, 1),
                        expandTransform
                };
            } else {
                if (view.Transform != null) {
                    group.Children = new Transform3DCollection {
                        new MatrixTransform3D () { Matrix = matrix },
                        expandTransform
                       };
                } else {
                    group.Children = new Transform3DCollection {
                        new TranslateTransform3D (view.X, view.Y, zOffset),
                        expandTransform
                    };
                }
            }
            Transform = group;
        }

        bool isFlattened = false;
        bool IsFlattened {
            get => isFlattened;
            set {
                isFlattened = value;
                expandTransform.ScaleZ = isFlattened ? 0.001 : 1;
            }
        }

        public bool IsHitTestVisible ()
            => !IsFlattened && ((VisualTreeHelper.GetParent (this) as InspectTreeNode3D)?.IsHitTestVisible () ?? true);

        void UpdateMaterial ()
        {
            if (material == null)
                return;

            var selected = Node.IsMouseOver ? SecondaryFocusColor : FocusColor;
            switch (material.Brush) {
            case SolidColorBrush solid:
                solid.Color = Node.IsSelected ? selected : (Node.IsMouseOver ? HoverColor : EmptyColor);
                break;
            default:
                material.Color = Node.IsSelected ? selected : (Node.IsMouseOver ? HoverColor : BlurColor);
                break;
            }
        }

        void IInspectTree3DNode<InspectTreeNode3D>.BuildPrimaryPlane (InspectTreeState state) =>
            BuildPrimaryPlane (state);

        public InspectTreeNode3D BuildChild (InspectTreeNode node, InspectTreeState state)
        {
            var child = new InspectTreeNode3D (node, state);
            node.Build3D (child, state);
            return child;
        }

        public void Add (InspectTreeNode3D child) =>
            Children.Add (child);
    }
}
