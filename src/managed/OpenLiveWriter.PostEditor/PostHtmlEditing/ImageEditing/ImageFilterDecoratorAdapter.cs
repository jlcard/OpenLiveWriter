// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using mshtml;
using OpenLiveWriter.Api;
using OpenLiveWriter.CoreServices;
using OpenLiveWriter.CoreServices.Diagnostics;
using OpenLiveWriter.Extensibility.BlogClient;
using OpenLiveWriter.Extensibility.ImageEditing;
using OpenLiveWriter.PostEditor.PostHtmlEditing.ImageEditing.Decorators;

namespace OpenLiveWriter.PostEditor.PostHtmlEditing
{
    /// <summary>
    /// Implements an ImageFilter
    /// </summary>
    internal class ImageFilterDecoratorAdapter : ImageDecoratorContext
    {
        Bitmap _originalImage;
        Bitmap _currImage;
        IProperties _currSettings;
        ImagePropertiesInfo _imageInfo;
        ImageDecoratorsList _decoratorsList;
        ImageEmbedType _embedType;
        IEditorOptions _editorOptions;

        private ImageFilterDecoratorAdapter(ImagePropertiesInfo imageInfo, ImageEmbedType embedType, ImageDecoratorInvocationSource invocationSource, IEditorOptions editorOptions)
        {
            _decoratorsList = imageInfo.ImageDecorators;
            _imageInfo = imageInfo;
            _embedType = embedType;
            _invocationSource = invocationSource;
            _editorOptions = editorOptions;
        }

        public static ImageFilter CreateImageDecoratorsFilter(ImagePropertiesInfo imageInfo, ImageEmbedType embedType, ImageDecoratorInvocationSource invocationSource, IEditorOptions editorOptions)
        {
            return new ImageFilter(new ImageFilterDecoratorAdapter(imageInfo, embedType, invocationSource, editorOptions).ApplyImageDecorators);
        }

        public Bitmap ApplyImageDecorators(Bitmap bitmap)
        {
            _currImage = bitmap;
            _originalImage = bitmap;

            bool borderNeedsReset = true;
            foreach (ImageDecorator decorator in _decoratorsList)
            {
                ApplyImageDecorator(decorator, _currImage, ref borderNeedsReset);
            }

            //update the rotation and size info in the image properties
            if (_embedType == ImageEmbedType.Embedded)
            {
                //                if (borderNeedsReset)
                //                    BorderMargin = ImageBorderMargin.Empty;

                if (!ImageHelper2.IsAnimated(_currImage))
                {
                    Size currImageSize = _currImage.Size;

                    //update the inline image size (don't forget to remove size added by borders)
                    ImageBorderMargin borderMargin = BorderMargin;
                    Size borderlessImageSize = new Size(
                        currImageSize.Width - borderMargin.Width,
                        currImageSize.Height - borderMargin.Height);
                    _imageInfo.InlineImageSize = borderlessImageSize;
                }
            }
            else
            {
                _imageInfo.LinkTargetImageSize = _currImage.Size;
            }

            _originalImage = null;
            return _currImage;
        }

        private void ApplyImageDecorator(ImageDecorator decorator, Bitmap bitmap, ref bool borderNeedsReset)
        {
            if (_embedType != ImageEmbedType.Embedded
                && (decorator.IsBorderDecorator || decorator.Id == TiltDecorator.Id))
            {
                return;
            }

            if (borderNeedsReset
                && _embedType == ImageEmbedType.Embedded
                && (decorator.IsBorderDecorator || decorator.Id == TiltDecorator.Id))
            {
                borderNeedsReset = false;
                //BorderMargin = ImageBorderMargin.Empty;
            }

            try
            {
                using (ApplicationPerformance.LogEvent("ApplyDecorator: " + decorator.DecoratorName))
                using (new WaitCursor())
                {
                    _currImage = bitmap;
                    _currSettings = _decoratorsList.GetImageDecoratorSettings(decorator);
                    decorator.Decorate(this);
                }
            }
            catch (Exception e)
            {
                Trace.Fail(String.Format(CultureInfo.InvariantCulture, "Failed to apply image decorator [{0}]: {1}", decorator.DecoratorName, e.ToString()));
            }
        }

        public Bitmap Image
        {
            get { return _currImage; }
            set
            {
                if (!ReferenceEquals(_currImage, value))
                {
                    if (_originalImage != _currImage)
                    {
                        //then the current image was generated by a decorator, and won't get disposed
                        //by the caller, so dispose it automatically;
                        _currImage.Dispose();
                    }
                    _currImage = value;
                }
            }
        }

        public IProperties Settings
        {
            get { return _currSettings; }
        }

        public IEditorOptions EditorOptions
        {
            get { return _editorOptions; }
        }

        public ImageEmbedType ImageEmbedType
        {
            get { return _embedType; }
        }

        public IHTMLElement ImgElement
        {
            get { return _imageInfo.ImgElement; }
        }

        public ImageDecoratorInvocationSource InvocationSource
        {
            get { return _invocationSource; }
        }
        private ImageDecoratorInvocationSource _invocationSource;

        public RotateFlipType ImageRotation
        {
            get
            {
                return _imageInfo.ImageRotation;
            }
        }

        public Uri SourceImageUri
        {
            get { return _imageInfo.ImageSourceUri; }
        }

        public float? EnforcedAspectRatio
        {
            get { return _decoratorsList.EnforcedAspectRatio; }
        }

        public ImageBorderMargin BorderMargin
        {
            get { return _imageInfo.InlineImageBorderMargin; }
            set { _imageInfo.InlineImageBorderMargin = value; }
        }
    }
}