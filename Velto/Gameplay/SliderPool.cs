using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto.Gameplay;

public class SliderPool
    {
        public struct SliderVertex
        {
            public Vector3 Position;
            public Vector2 UV;
            public float Progress; // 0..1 along slider
        }


        private uint _capacity;
        private Renderer _renderer;
        private Queue<Slider> _sliderQueue = new();

        public SliderPool(Renderer renderer, uint capacity = 100)
        {
            _renderer = renderer;
            _capacity = 100;
        }

        public void QueueSlider(Slider slider)
        {
            if (!_sliderQueue.Contains(slider)) _sliderQueue.Enqueue(slider);
        }


        // ball Vector2 pos = slider.GetPointAt(progress);
        public void Update(double songCursor, float osuRadius)
        {
            /*foreach (var pair in _framebuffers)
            {
                if (pair.Key.Time < songCursor - 15000)
                {
                    _framebuffers[pair.Key].Dispose();
                    _framebuffers.Remove(pair.Key);
                }
            }*/

            if (_sliderQueue.TryPeek(out Slider slider))
            {
                float minX = slider.Points.Min(p => p.X);
                float maxX = slider.Points.Max(p => p.X);
                float minY = slider.Points.Min(p => p.Y);
                float maxY = slider.Points.Max(p => p.Y);

                float radius = osuRadius;
                minX -= radius;
                maxX += radius;
                minY -= radius;
                maxY += radius;
                maxY += radius;

                slider.CacheOffset = new Vector2(minX, minY);
                float w = (float)Math.Ceiling(maxX - minX);
                float h = (float)Math.Ceiling(maxY - minY);

                var (vboData, iboData) = BuildSliderMesh(slider.Points, radius);
                slider.Vbo = new BufferObject<float>(vboData, BufferTarget.ArrayBuffer, BufferUsage.DynamicDraw);
                slider.Ebo = new BufferObject<uint>(iboData, BufferTarget.ElementArrayBuffer, BufferUsage.DynamicDraw);
                slider.Vao = new VertexArrayObject<float, uint>(slider.Vbo, slider.Ebo);

                slider.IndexCount = iboData.Length;

                slider.Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 6 * sizeof(float),
                    0); // position
                slider.Vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 6 * sizeof(float),
                    3 * sizeof(float)); // uv
                slider.Vao.VertexAttributePointer(2, 1, VertexAttribPointerType.Float, 6 * sizeof(float),
                    5 * sizeof(float)); // progress

                slider.SliderFramebuffer = new(_renderer, (int)w, (int)h);

                _renderer.FixFramebuffer();
                _sliderQueue.Dequeue();
            }
        }

        public static (float[] vbo, uint[] ibo) BuildSliderMesh(
            List<Vector2> points,
            float radius)
        {
            var vertices = new List<SliderVertex>();
            var indices = new List<uint>();

            float totalLength = 0f;
            float[] segLen = new float[points.Count];

            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                segLen[i] = totalLength;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 prev = points[Math.Max(0, i - 1)];
                Vector2 curr = points[i];
                Vector2 next = points[Math.Min(points.Count - 1, i + 1)];

                var d1 = curr - prev;
                var d2 = next - curr;

                if (d1.LengthSquared < 0.0001f) d1 = d2;
                if (d2.LengthSquared < 0.0001f) d2 = d1;

                d1 = d1.LengthSquared < 0.0001f ? Vector2.UnitX : Vector2.Normalize(d1);
                d2 = d2.LengthSquared < 0.0001f ? Vector2.UnitX : Vector2.Normalize(d2);

                var dir = d1 + d2;
                dir = dir.LengthSquared < 0.0001f ? d2 : Vector2.Normalize(dir);

                Vector2 normal = new Vector2(-dir.Y, dir.X);

                float t = segLen[i] / Math.Max(1, totalLength);

                Vector2 left = curr + normal * radius;
                Vector2 right = curr - normal * radius;

                vertices.Add(new SliderVertex
                {
                    Position = new Vector3(left.X, left.Y, 0),
                    UV = new Vector2(0, t),
                    Progress = t
                });

                vertices.Add(new SliderVertex
                {
                    Position = new Vector3(right.X, right.Y, 0),
                    UV = new Vector2(1, t),
                    Progress = t
                });
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                uint i0 = (uint)(i * 2);
                uint i1 = i0 + 1;
                uint i2 = i0 + 2;
                uint i3 = i0 + 3;

                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);

                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i3);
            }

            return (
                vertices.SelectMany(v => new float[]
                {
                    v.Position.X, v.Position.Y, v.Position.Z,
                    v.UV.X, v.UV.Y,
                    v.Progress
                }).ToArray(),
                indices.ToArray()
            );
        }

        public void Drain()
        {
            /*foreach (var pair in _framebuffers)
            {
                _framebuffers[pair.Key].Dispose();
                _framebuffers.Remove(pair.Key);
            }*/
            _sliderQueue.Clear();
        }
    }