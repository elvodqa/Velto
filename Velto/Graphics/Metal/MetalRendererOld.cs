    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using OpenTK.Mathematics;
    using SharpMetal.Foundation;
    using SharpMetal.Metal;
    using SharpMetal.QuartzCore;
    using Velto.Core;
    using Velto.Graphics.OpenGL;

    namespace Velto.Graphics.Metal;

    [SupportedOSPlatform("macos")]
    public unsafe class MetalRendererOld : IRenderer
    {
        private float[] vertices = {
            // first triangle
            1.0f,  0.0f, 0.0f, // top right
            1.0f,  1.0f, 0.0f, // bottom right
            0.0f,  0.0f, 0.0f, // top left
            // second triangle
            1.0f,  1.0f, 0.0f, // bottom right
            0.0f,  1.0f, 0.0f, // bottom left
            0.0f,  0.0f, 0.0f  // top left
        };
        
        private float[] uvs = {
            // first triangle
            1.0f, 0.0f,  // top right
            1.0f, 1.0f,  // bottom right
            0.0f, 0.0f,  // top left

            // second triangle
            1.0f, 1.0f,  // bottom right
            0.0f, 1.0f,  // bottom left
            0.0f, 0.0f,  // top left
        };

        private enum DrawType : int
        {
            Primitive = 1,
            Texture = 2,
            MSDF = 3,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private unsafe struct DrawCall
        {
            public DrawType Type;
            public Matrix4 Model;           // You'll likely need to transpose this before copying
            public Color4<Rgba> Color;
            public MTLResourceID Texture;   // Must match exactly what shader expects
            public float CornerRadius;
            public float DistanceRange;
            public float SmoothingScale;
            public Vector4 UV;              // or struct {float u0,v0,u1,v1;}
            public Vector2 GlyphSize;
        
            // Add padding if needed to make size multiple of 16
        }
        
        private class DrawCallSet
        {
            public MetalFramebuffer? Framebuffer { get; set; }
            public Color4<Rgba>? ClearColor { get; set; }        // null = don't clear
            public ScissorRect ScissorRect { get; set; }
            public Matrix4 View { get; set; }
            public Matrix4 Projection { get; set; }

            public List<DrawCall> DrawCalls { get; } = new();

            // Optional: you can add more later (depth state, blend state, etc.)
        }
        
        public Window Window { get; }
        public uint DrawCallCount { get; private set; }

        private Matrix4 CreateDefaultProjection()
        {
            var size = Window.WindowSize;
            // Top-left origin, common for 2D/UI
            return Matrix4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, -1f, 1f);
        }
        
        // tl;dr memcpy(buffer.contents(), source, sizeof(source));
        public static unsafe void CopyToBuffer<T>(T[] source, MTLBuffer buffer)
        {
            var span = new Span<T>(buffer.Contents.ToPointer(), source.Length);
            source.CopyTo(span);
        }
        
        private Stack<ScissorRect> ScissorStack = new();
        private Stack<MetalFramebuffer> FramebufferStack = new();
        private List<DrawCallSet> DrawCallSets = new();
        private MetalFramebuffer? Framebuffer = null;
        private MetalGraphicsDevice device;
        private ITexture whiteTexture;
        private ITexture circleTexture;

        private MTLBuffer vertexBuffer;
        private MTLBuffer uvBuffer;
        private MTLBuffer drawCallBuffer;
        private MTLRenderPipelineState renderPipelineState;
        private MTLCommandQueue commandQueue;
        private List<MTLTexture> textures = new();

        public MetalRendererOld(MetalGraphicsDevice device, Window window)
        {
            this.device = device;
            Window = window;
            whiteTexture = device.CreateTexture(Resources.GetPath("Resources/Textures/white.png"));
            circleTexture = device.CreateTexture(Resources.GetPath("Resources/Textures/circle.png"));

            vertexBuffer = this.device.Device.NewBuffer((ulong)(sizeof(float) * vertices.Length),
                MTLResourceOptions.ResourceStorageModeShared);
            uvBuffer = this.device.Device.NewBuffer((ulong)(sizeof(float) * uvs.Length),
                MTLResourceOptions.ResourceStorageModeShared);
            
            CopyToBuffer(vertices, vertexBuffer);
           /* vertexBuffer.DidModifyRange(new NSRange()
            {
                location = 0,
                length = (ulong)(vertices.Length * sizeof(float))
            });*/
            
            CopyToBuffer(uvs, uvBuffer);
            /*uvBuffer.DidModifyRange(new NSRange()   
            {
                location = 0,
                length = (ulong)(uvs.Length * sizeof(float))
            });*/
            
            drawCallBuffer =
                this.device.Device.NewBuffer((ulong)(sizeof(DrawCall) * 2048), MTLResourceOptions.ResourceStorageModeShared);


            NSString shaderSource =
                NSString.String(File.ReadAllText(Resources.GetPath("Resources/Shaders/ubershader.metal")));
            NSError libraryError = new NSError();
            var library = this.device.Device.NewLibrary(shaderSource, new MTLCompileOptions(), ref libraryError);
            if (libraryError != IntPtr.Zero)
            {
                Logger.Instance.Error($"{libraryError.LocalizedDescription}");
                throw new Exception($"{libraryError.LocalizedDescription}");
            }
            
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor()
            {
                Label = "Metal Pipeline",
                VertexFunction = library.NewFunction("vertex_main0"),
                FragmentFunction = library.NewFunction("fragment_main0"),
            };
            var cd = renderPipelineDescriptor.ColorAttachments.Object(0);
            cd.PixelFormat = MTLPixelFormat.RGBA8Unorm;
            cd.BlendingEnabled = true;
            
            cd.SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
            cd.DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            cd.RgbBlendOperation = MTLBlendOperation.Add;
            
            cd.SourceAlphaBlendFactor = MTLBlendFactor.One;
            cd.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            cd.AlphaBlendOperation = MTLBlendOperation.Add;
            
            renderPipelineDescriptor.ColorAttachments.SetObject(cd, 0);
            
            NSError pipelineError = new NSError();
            renderPipelineState = this.device.Device.NewRenderPipelineState(renderPipelineDescriptor, ref pipelineError);
            if (pipelineError != IntPtr.Zero)
            {
                Logger.Instance.Error($"{pipelineError.LocalizedDescription}");
                throw new Exception($"{pipelineError.LocalizedDescription}");
            }
            
            shaderSource.Dispose();
            pipelineError.Dispose();
            renderPipelineDescriptor.Dispose();

            commandQueue = this.device.Device.NewCommandQueue();
        }

        public void PushScissor(ScissorRect r)
        {
            // Compute intersection with current scissor
            ScissorRect current = ScissorStack.Count > 0 
                ? ScissorStack.Peek() 
                : new ScissorRect(0, 0, (int)Window.WindowSize.X, (int)Window.WindowSize.Y);

            int x1 = Math.Max(current.X, r.X);
            int y1 = Math.Max(current.Y, r.Y);
            int x2 = Math.Min(current.X + current.W, r.X + r.W);
            int y2 = Math.Min(current.Y + current.H, r.Y + r.H);

            var newRect = new ScissorRect
            {
                X = x1,
                Y = y1,
                W = Math.Max(0, x2 - x1),
                H = Math.Max(0, y2 - y1)
            };

            ScissorStack.Push(newRect);
            StartNewSetWithCurrentState(); // important!
        }

        public void PushScissor(int x, int y, int w, int h)
        {
            PushScissor(new ScissorRect(x, y, w, h));
        }

        public void PopScissor()
        {
            if (ScissorStack.Count == 0) return;
            ScissorStack.Pop();

            ScissorRect newRect = ScissorStack.Count > 0 
                ? ScissorStack.Peek()
                : new ScissorRect(0, 0, (int)Window.WindowSize.X, (int)Window.WindowSize.Y);

            SetScissor(newRect); // or call StartNewSetWithCurrentState()
        }
        
        private void StartNewSetWithCurrentState(Color4<Rgba>? clearColor = null)
        {
            var last = DrawCallSets[^1];

            var newSet = new DrawCallSet
            {
                Framebuffer = Framebuffer,
                ClearColor = clearColor ?? last.ClearColor, // only override if explicitly passed
                ScissorRect = last.ScissorRect,
                View = last.View,
                Projection = last.Projection,
            };

            // If scissor actually changed, we can decide whether to force new pass or not.
            // For now, always new set on scissor change (simple + correct)
            DrawCallSets.Add(newSet);
        }

        private void SetScissor(ScissorRect rect)
        {
            var last = DrawCallSets[^1];
            if (last.ScissorRect.Equals(rect)) return; // no change

            StartNewSetWithCurrentState();
            DrawCallSets[^1].ScissorRect = rect;
        }
        
        public void PushFramebuffer(IFramebuffer fb)
        {
            var metalFb = fb as MetalFramebuffer;
            FramebufferStack.Push(metalFb);
            Framebuffer = metalFb;

            StartNewSetWithCurrentState(); // New render pass!
        }

        public void PopFramebuffer()
        {
            if (FramebufferStack.Count == 0) return;
            FramebufferStack.Pop();

            Framebuffer = FramebufferStack.TryPeek(out var prev) ? prev : null;
            StartNewSetWithCurrentState();
        }

        public void BeginFrame()
        {
            textures.Clear();
            FramebufferStack.Clear();
            ScissorStack.Clear();
            DrawCallSets.Clear();
        
            Framebuffer = null; // or default backbuffer

            DrawCallSets.Add(new DrawCallSet
            {
                Framebuffer = null, // backbuffer
                ClearColor = null,  // usually cleared by window or explicit Clear()
                ScissorRect = new ScissorRect(0, 0, (int)Window.WindowSize.X, (int)Window.WindowSize.Y),
                View = Matrix4.Identity,
                Projection = CreateDefaultProjection(),
            });
        }

        public void SetupRenderPassAttachments(MTLRenderPassDescriptor rpd, MetalFramebuffer? framebuffer,
            Color4<Rgba>? clearColor, CAMetalDrawable drawable)
        {
            var colorAttachment = rpd.ColorAttachments.Object(0);

            colorAttachment.Texture = framebuffer != null ? framebuffer.Handle : drawable.Texture;
            colorAttachment.LoadAction = clearColor != null ? MTLLoadAction.Clear : MTLLoadAction.Load;
            colorAttachment.StoreAction = MTLStoreAction.Store;
            if (clearColor != null)
            {
                colorAttachment.ClearColor = new MTLClearColor()
                {
                    red = clearColor.Value.X,
                    green = clearColor.Value.Y,
                    blue = clearColor.Value.Z,
                    alpha = clearColor.Value.W,
                };
            }

            rpd.ColorAttachments.SetObject(colorAttachment, 0);
        }
        
        public void EndFrame()
        {
            var drawable = device.MetalLayer.NextDrawable;
            if (drawable == null) return;

            var commandBuffer = commandQueue.CommandBuffer();

            foreach (var set in DrawCallSets)
            {
                // Skip completely empty passes that do nothing (no clear + no draws)
                if (set.DrawCalls.Count == 0 && set.ClearColor == null)
                    continue;

                var rpd = new MTLRenderPassDescriptor();
                SetupRenderPassAttachments(rpd, set.Framebuffer, set.ClearColor, drawable);

                var encoder = commandBuffer.RenderCommandEncoder(rpd);

                encoder.SetScissorRect(new MTLScissorRect
                {
                    x = (ulong)set.ScissorRect.X,
                    y = (ulong)set.ScissorRect.Y,
                    width = (ulong)set.ScissorRect.W,
                    height = (ulong)set.ScissorRect.H
                });

                encoder.SetRenderPipelineState(renderPipelineState);

                // Use resources (textures)
                foreach (var texture in textures)
                {
                    encoder.UseResource(texture, MTLResourceUsage.Read, MTLRenderStages.RenderStageFragment);
                }

                // Only do vertex setup + draw if we actually have draw calls
                if (set.DrawCalls.Count > 0)
                {
                    CopyToBuffer(set.DrawCalls.ToArray(), drawCallBuffer);
                    /*drawCallBuffer.DidModifyRange(new NSRange()
                    {
                        location = 0,
                        length = (ulong)(set.DrawCalls.Count * sizeof(DrawCall))
                    });*/

                    encoder.SetVertexBuffer(vertexBuffer, 0, 0);
                    encoder.SetVertexBuffer(uvBuffer, 0, 1);

                    var proj = set.Projection;
                    encoder.SetVertexBytes((nint)(&proj), (ulong)sizeof(Matrix4), 2);

                    encoder.SetVertexBuffer(drawCallBuffer, 0, 3);
                    encoder.SetFragmentBuffer(drawCallBuffer, 0, 0);

                    encoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, 6, (ulong)set.DrawCalls.Count);
                }

                encoder.EndEncoding();
                rpd.Dispose();
            }

            commandBuffer.PresentDrawable(drawable);
            commandBuffer.Commit();
        }
        
        public void Clear(Color4<Rgba> color)
        {
            StartNewSetWithCurrentState(clearColor: color);
        }

        public void DrawTexture(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
        {
            var model =
                Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
                Matrix4.CreateScale(size.X, size.Y, 1f) *
                Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-rotation)) *
                Matrix4.CreateTranslation(position.X + size.X / 2f, position.Y + size.Y / 2f, 0f);
            
            DrawCallSets.Last().DrawCalls.Add(new DrawCall()
            {
                Type = DrawType.Texture,
                Texture = (texture as MetalTexture).Handle.GpuResourceID,
                Color = color,
                CornerRadius = 0,
                Model = model,
            });
            
            textures.Add((texture as MetalTexture).Handle);
        }

        public void DrawFramebuffer(IFramebuffer framebuffer, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
        {
            var texture = framebuffer as MetalFramebuffer;
            var model =
                Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
                Matrix4.CreateScale(size.X, size.Y, 1f) *
                Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-rotation)) *
                Matrix4.CreateTranslation(position.X + size.X / 2f, position.Y + size.Y / 2f, 0f);
            
            DrawCallSets.Last().DrawCalls.Add(new DrawCall()
            {
                Type = DrawType.Texture,
                Texture = texture.Handle.GpuResourceID,
                Color = color,
                CornerRadius = 0,
                Model = model,
            });
            
            textures.Add(texture.Handle);
        }

        public void DrawRectangle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
        {
            DrawTexture(whiteTexture, position, size, color, rotation);
        }

        public void DrawCircle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
        {
            DrawTexture(circleTexture, position, size, color, rotation);
        }

        public void DrawText(Font font, string text, Vector2 position, float pixelLineHeight, Color4<Rgba> color)
        {
            // 
        }

        public void DrawTextWrapped(Font font, string text, Vector2 position, float pixelLineHeight, float maxWidth, Color4<Rgba> color)
        {
            //
        }

        public void DrawTextCentered(Font font, string text, Vector2 center, float pixelLineHeight, Color4<Rgba> color)
        {
            //
        }

        public void DrawTextWrappedCentered(Font font, string text, Vector2 center, float pixelLineHeight, float maxWidth,
            Color4<Rgba> color)
        {
            //
        }

        public void FlushText(Font font)
        {
            //
        }
        
        public void Dispose()
        {
            
        }

    }