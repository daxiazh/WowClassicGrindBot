import ScreenCaptureKit
import CoreVideo
import Foundation
import AVFoundation

/// 帧数据回调类型
/// - Parameters:
///   - data: 像素数据指针 (BGRA32 格式)
///   - width: 图像宽度
///   - height: 图像高度
///   - bytesPerRow: 每行字节数
public typealias FrameCallback = @convention(c) (UnsafePointer<UInt8>, Int32, Int32, Int32) -> Void

/// ScreenCaptureKit 管理类
class ScreenCaptureManager: NSObject, SCStreamOutput, SCStreamDelegate {
    private var stream: SCStream?
    private var frameCallback: FrameCallback?
    private var isCapturing = false
    
    /// 初始化管理器
    /// - Parameter callback: 帧数据回调函数
    init(callback: @escaping FrameCallback) {
        self.frameCallback = callback
        super.init()
    }
    
    /// 设置并启动捕获流
    /// - Parameter windowID: 窗口 ID
    func setupStream(windowID: UInt32) async {
        do {
            // 1. 检查 Screen Recording 权限
            print("ScreenCaptureKit: Checking screen recording permission...")
            let canRecord = await checkScreenRecordingPermission()
            if !canRecord {
                print("ScreenCaptureKit: ERROR - Screen Recording permission not granted!")
                print("ScreenCaptureKit: Please enable in System Settings > Privacy & Security > Screen Recording")
                return
            }
            print("ScreenCaptureKit: Screen recording permission OK")
            
            // 2. 获取可共享内容
            print("ScreenCaptureKit: Requesting shareable content...")
            let content = try await SCShareableContent.current
            print("ScreenCaptureKit: Found \(content.windows.count) windows")
            
            // 3. 查找目标窗口
            guard let window = content.windows.first(where: { $0.windowID == windowID }) else {
                print("ScreenCaptureKit: Window not found: \(windowID)")
                print("ScreenCaptureKit: Available windows:")
                for w in content.windows.prefix(5) {
                    print("  - ID: \(w.windowID), Title: \(w.title ?? "nil"), Size: \(w.frame.width)x\(w.frame.height)")
                }
                return
            }
            
            print("ScreenCaptureKit: Found window: \(window.title ?? "Untitled") (\(window.frame.width)x\(window.frame.height))")
            
            // 4. 创建内容过滤器
            let filter = SCContentFilter(desktopIndependentWindow: window)
            // 根据 filter.contentRect 和 filter.pointPixelScale 计算像素尺寸
            let logicalRect = filter.contentRect
            let scale = CGFloat(filter.pointPixelScale)            
            let pixelWidth  = logicalRect.width  * scale
            let pixelHeight = logicalRect.height * scale
            
            // 5. 配置流
            let config = SCStreamConfiguration()
            config.width = Int(pixelWidth)
            config.height = Int(pixelHeight)
            config.colorSpaceName = CGColorSpace.sRGB
            config.pixelFormat = kCVPixelFormatType_32BGRA  // BGRA 格式,与 WowScreenDXGI 一致
            config.minimumFrameInterval = CMTime(value: 1, timescale: 60)  // 60 FPS
            config.queueDepth = 2  // 增加缓冲
            config.showsCursor = false  // 不显示鼠标
            // config.captureResolution = .nominal
            // config.scalesToFit = true
            
            print("ScreenCaptureKit: Creating filter and stream...")
            
            // 6. 创建流
            stream = SCStream(filter: filter, configuration: config, delegate: self)
            
            // 7. 添加输出(使用全局队列以避免主线程阻塞)
            let queue = DispatchQueue(label: "com.screencapture.output", qos: .userInteractive)
            try stream?.addStreamOutput(self, type: .screen, sampleHandlerQueue: queue)
            
            print("ScreenCaptureKit: Starting capture...")
            
            // 8. 启动捕获
            try await stream?.startCapture()
            isCapturing = true
            
            print("ScreenCaptureKit: Stream started successfully")
            
        } catch let error as NSError {
            print("ScreenCaptureKit: Failed to setup stream: \(error)")
            print("ScreenCaptureKit: Error domain: \(error.domain), code: \(error.code)")
            print("ScreenCaptureKit: User info: \(error.userInfo)")
        } catch {
            print("ScreenCaptureKit: Failed to setup stream: \(error)")
        }
    }
    
    /// SCStreamOutput 协议方法 - 接收帧
    /// - Parameters:
    ///   - stream: 流对象
    ///   - sampleBuffer: 样本缓冲区
    ///   - type: 输出类型
    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else {
            return
        }
        
        // 锁定像素缓冲区
        CVPixelBufferLockBaseAddress(pixelBuffer, .readOnly)
        defer { CVPixelBufferUnlockBaseAddress(pixelBuffer, .readOnly) }
        
        let width = CVPixelBufferGetWidth(pixelBuffer)
        let height = CVPixelBufferGetHeight(pixelBuffer)
        let bytesPerRow = CVPixelBufferGetBytesPerRow(pixelBuffer)
        
        // 获取像素数据并回调到 C#
        if let baseAddress = CVPixelBufferGetBaseAddress(pixelBuffer) {
            let data = baseAddress.assumingMemoryBound(to: UInt8.self)
            frameCallback?(data, Int32(width), Int32(height), Int32(bytesPerRow))
        }
    }
    
    /// 停止捕获流
    func stopCapture() {
        if isCapturing {
            Task {
                do {
                    try await stream?.stopCapture()
                    isCapturing = false
                    print("ScreenCaptureKit: Stream stopped")
                } catch {
                    print("ScreenCaptureKit: Failed to stop stream: \(error)")
                }
            }
        }
    }
    
    // MARK: - SCStreamDelegate
    
    func stream(_ stream: SCStream, didStopWithError error: Error) {
        print("ScreenCaptureKit: Stream stopped with error: \(error)")
        isCapturing = false
    }
    
    /// 检查 Screen Recording 权限
    /// - Returns: 是否已授予权限
    private func checkScreenRecordingPermission() async -> Bool {
        // macOS 12.3+ 使用 SCShareableContent.excludingDesktopWindows
        // 如果没有权限,此调用会返回空列表或抛出错误
        do {
            let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
            // 如果能获取到内容,说明有权限
            return content.windows.count > 0 || content.displays.count > 0
        } catch {
            print("ScreenCaptureKit: Permission check failed: \(error)")
            return false
        }
    }
}

// MARK: - C 导出函数

/// 创建屏幕捕获流
/// - Parameters:
///   - windowID: 目标窗口 ID
///   - callback: 帧数据回调函数
/// - Returns: 管理器句柄,失败返回 nil
@_cdecl("sc_create_stream")
public func sc_create_stream(windowID: UInt32, callback: @escaping FrameCallback) -> UnsafeMutableRawPointer? {
    let manager = ScreenCaptureManager(callback: callback)
    
    // 同步等待流初始化完成
    let semaphore = DispatchSemaphore(value: 0)
    Task {
        await manager.setupStream(windowID: windowID)
        semaphore.signal()
    }
    
    // 等待最多 3 秒
    _ = semaphore.wait(timeout: .now() + 3)
    
    // 返回非托管指针给 C#
    return Unmanaged.passRetained(manager).toOpaque()
}

/// 停止捕获流
/// - Parameter handle: 管理器句柄
@_cdecl("sc_stop_stream")
public func sc_stop_stream(_ handle: UnsafeMutableRawPointer) {
    let manager = Unmanaged<ScreenCaptureManager>.fromOpaque(handle).takeRetainedValue()
    manager.stopCapture()
}
