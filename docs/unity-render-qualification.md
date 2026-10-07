<a id="작은-unity의-미리보기-검사"></a>
# Preview checks for a small Unity project

[한국어](ko/unity-render-qualification.md)

The sibling `splinter-unity/Assets/Editor/SplinterQualificationProbe.cs` and `SplinterQualificationRender.cs` are development fixtures that inspect the selected Qualification scene. They accept commands only in the test Editor explicitly identified by `-splinterControlPath`; they are not product workspace validation APIs. See the [FR record](verification/first-render-qualification.md) for actual execution scope and limitations.

<a id="요청과-성공-조건"></a>
## Requests and success conditions

Atomically publish a new `id` and `action: "inspect"` to the owned control's `qualification-command.json`. In `qualification-response.json`, check the matching `requestId`, actual `projectPath`, PID, generation, assembly MVID, `ready`, and `success` together. A successful response alone does not turn an old fixture PNG into new rendering evidence.

The new inspection response's `assets.previewContract` is `qualification-urp-v1`. `assets.previewPath` is a new PNG path containing the request ID's SHA-256; do not look for a fixed `qualification-preview.png`. Interpret the following fields together with the actual PNG.

| Field | Contract |
| --- | --- |
| `previewPath`, `previewSha256` | Successfully published per-request PNG and SHA-256. If the path already exists, fail without overwriting. |
| `previewWidth`, `previewHeight` | 640×480 readback result. |
| `foregroundPixels` | Pixels whose summed RGB difference from the fixed solid background exceeds 24 and whose alpha is at least 250. At least 64 are required. |
| `renderPipelineBefore`, `renderPipeline` | Actual pipeline type before the request and after checking support. Initial absence is `uninitialized`. |
| `renderPasses`, `renderMilliseconds` | At most two synchronous render requests and elapsed time through resource cleanup, separate from Editor ready/import time. |
| `foregroundPixelsPerPass` | Foreground pixel count actually read in each pass. The first empty pass is retained; two empty passes fail. |
| `sceneStateRestored` | Existing scene count and active scene restored after closing the temporarily opened Qualification scene. |

After checking mesh/texture imports and the scene's mesh reference, check URP `SingleCameraRequest` support and render through `SubmitRenderRequest`. Disable asynchronous shader compilation only while rendering, then restore its previous value. Clear the temporary target to transparent before each pass to avoid reusing GPU pixels from an earlier request. Check the image using the fixed background and corners. If the first pass is empty, render only once more within the same request. Preserve each pass's `foregroundPixelsPerPass`; fail if both are empty. This check detects a blank image in the defined fixture; it does not perform general image-semantic evaluation or guarantee visual correctness of every material. Also inspect the saved PNG to confirm the model is visible.

Open the scene as a separate additive instance and explicitly assign the camera's scene. Fail if the Qualification scene is already open or the pipeline is not URP. Do not close or save existing unsaved scenes. For a request that begins rendering, clean up/restore temporary images, RenderTextures, scenes, the global active RenderTexture, and shader-compilation settings on both success and failure. Rejection before rendering, such as an output collision, returns the default `sceneStateRestored:false` without touching scenes. Publish the result by flushing a new temporary file in the same directory and moving it without overwrite. Publication errors fail the inspection; an earlier PNG is not reused as a successful result.

<a id="제품-경계"></a>
## Product boundary

`unity-refresh` in [snapshot-spike](snapshot-overlay.md) checks request/fingerprint/compile success and loaded code. The fixture's `inspect` separately checks the model, textures, and PNG in that Editor. These success conditions do not replace each other. The fixture command is not added to Core/Runtime journals/flush, Windows callbacks, or product schemas/capabilities.

The supported API follows Unity 6000.3's [URP render requests](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/User-Render-Requests.html). Consult FR execution records for actual verification of new cold starts, normal restarts, reloads, and failure paths.
