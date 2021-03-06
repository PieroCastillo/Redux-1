﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Redux.Vulkan
{
    public unsafe class Adapter
    {
        internal bool? _supportInitializad;
        internal VkInstance instance;

        internal vkDebugUtilsMessengerCallbackEXT _debugMessengerCallbackFunc;
        internal VkDebugUtilsMessengerEXT _debugMessenger = VkDebugUtilsMessengerEXT.Null;

        internal uint instance_extensions_count;
        internal uint device_count; // number of GPUs we're rendering to --- if DG is disabled, this is 1


        internal List<string> device_extensions_names { get; private set; } = new();


        internal VkPhysicalDevice handle;
        internal VkPhysicalDevice[] handles;
        internal VkPhysicalDeviceProperties device_properties;



        public Adapter()
        {
            Recreate();
        }


        public string DeviceName
        {
            get
            {
                VkPhysicalDeviceProperties properties = device_properties;
                return Interop.GetString(properties.deviceName);
            }
        }

        public string Description
        {
            get
            {
                VkPhysicalDeviceProperties properties = device_properties;
                return Interop.GetString(properties.deviceName) + $" - {VendorNameString(VendorId)}";
            }
        }

        public uint VendorId => device_properties.vendorID;

        public List<string> InstanceExtensionsNames { get; private set; } = new();

        public List<string> ValidationLayer { get; private set; } = new();

        public bool SupportsVulkan11Instance { get; private set; }
        public bool SupportsVulkan11Device { get; private set; }
        public bool SupportsPhysicalDeviceProperties2 { get; private set; }
        public bool SupportsWin32Surface { get; private set; }

        public bool IsSupported()
        {
            if (_supportInitializad.HasValue)
                return _supportInitializad.Value;

            try
            {
                VkResult result = vkInitialize();
                _supportInitializad = result == VkResult.Success;
                return _supportInitializad.Value;
            }
            catch
            {
                _supportInitializad = false;
                return false;
            }
        }



        public void Recreate()
        {


            if (!IsSupported())
                throw new NotSupportedException("Vulkan is not supported");

            supports_extensions();

            CreateInstance(InstanceExtensionsNames.ToArray());

            CreatePhysicalDevice();

            CreatePhysicalDeviceProperties();

            device_extension();


            SupportsVulkan11Instance = vkEnumerateInstanceVersion() >= VkVersion.Version_1_1;


            if (device_properties.apiVersion >= VkVersion.Version_1_1)
                SupportsVulkan11Device = SupportsVulkan11Instance;

        }

        internal void CreatePhysicalDeviceProperties()
        {
            vkGetPhysicalDeviceProperties(handle, out device_properties);
        }

        internal void device_extension()
        {
            foreach (VkExtensionProperties item in vkEnumerateDeviceExtensionProperties(handle))
            {
                device_extensions_names.Add(Interop.GetString(item.extensionName));
            }
        }

        internal void supports_extensions()
        {
            IEnumerable<string> instance_extensions_names = Instance_Extensions()
                                                    .ToArray()
                                                    .Select(_ => Interop.GetString(_.extensionName));


            if (instance_extensions_names.Contains("VK_EXT_debug_report"))
            {
                InstanceExtensionsNames.Add("VK_EXT_debug_report");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (instance_extensions_names.Contains("VK_KHR_win32_surface"))
                {
                    InstanceExtensionsNames.Add("VK_KHR_win32_surface");
                    SupportsWin32Surface = true;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (instance_extensions_names.Contains("VK_MVK_macos_surface"))
                {
                    InstanceExtensionsNames.Add("VK_MVK_macos_surface");
                    //SupportsMacOSSurface = true;
                }

                if (instance_extensions_names.Contains("VK_MVK_ios_surface"))
                {
                    InstanceExtensionsNames.Add("VK_MVK_ios_surface");
                    //SupportsMacOSSurface = true;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (instance_extensions_names.Contains("VK_KHR_android_surface"))
                {
                    InstanceExtensionsNames.Add("VK_KHR_android_surface");
                    //SupportsAndroidSurface = true;
                }

                if (instance_extensions_names.Contains("VK_KHR_xlib_surface"))
                {
                    InstanceExtensionsNames.Add("VK_KHR_xlib_surface");
                    //SupportsX11Surface = true;
                }

                if (instance_extensions_names.Contains("VK_KHR_wayland_surface"))
                {
                    InstanceExtensionsNames.Add("VK_KHR_wayland_surface");
                    //SupportsWaylandSurface = true;
                }
            }

            if (instance_extensions_names.Contains("VK_KHR_surface"))
            {
                InstanceExtensionsNames.Add("VK_KHR_surface");
                //SupportsSurface = true;
            }

            if (instance_extensions_names.Contains("VK_KHR_get_physical_device_properties2"))
            {
                InstanceExtensionsNames.Add("VK_KHR_get_physical_device_properties2");
                SupportsPhysicalDeviceProperties2 = true;
            }
        }

        internal void CreateInstance(string[] extensions)
        {

            VkApplicationInfo app_info = new()
            {
                sType = VkStructureType.ApplicationInfo,
                pNext = null,
                apiVersion = new VkVersion(1, 0, 0),
                applicationVersion = new VkVersion(0, 0, 1),
                engineVersion = new VkVersion(),
            };


            // TODO: layers
            string[] layers = new[]
            {
                "VK_LAYER_KHRONOS_validation",
            };

            VkStringArray string_array = new VkStringArray(extensions);

            VkInstanceCreateInfo inst_info = new()
            {
                sType = VkStructureType.InstanceCreateInfo,
                pNext = null,
                flags = VkInstanceCreateFlags.None,
                pApplicationInfo = &app_info,
                ppEnabledExtensionNames = string_array,
                enabledExtensionCount = string_array.Length,
            };

            VkDebugUtilsMessengerCreateInfoEXT debugUtilsCreateInfo = new()
            {
                sType = VkStructureType.DebugUtilsMessengerCreateInfoEXT,
                pNext = null,
                flags = VkDebugUtilsMessengerCreateFlagsEXT.None,
                pUserData = null,
                messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Error | VkDebugUtilsMessageSeverityFlagsEXT.Warning | VkDebugUtilsMessageSeverityFlagsEXT.Info,
                messageType = VkDebugUtilsMessageTypeFlagsEXT.Validation | VkDebugUtilsMessageTypeFlagsEXT.Performance,
                //pfnUserCallback = Interop.GetFunctionPointerForDelegate(_debugMessengerCallbackFunc = DebugMessengerCallback),
            };
            // TODO: DebugMessengerCallback
            if (false)
            {
                inst_info.pNext = &debugUtilsCreateInfo;
                //inst_info.ppEnabledLayerNames = Interop.String.AllocToPointers(layers);
                inst_info.enabledLayerCount = (uint)layers.Length;

            }

            vkCreateInstance(&inst_info, null, out instance);
            vkLoadInstance(instance);

            //vkCreateDebugUtilsMessengerEXT(instance, &debugUtilsCreateInfo, null, out _debugMessenger).CheckResult();
        }




        internal void CreatePhysicalDevice()
        {
            // Physical Device
            uint count = 0;
            vkEnumeratePhysicalDevices(instance, &count, null);
            VkPhysicalDevice* physicalDevicesptr = stackalloc VkPhysicalDevice[(int)count];
            vkEnumeratePhysicalDevices(instance, &count, physicalDevicesptr);

            device_count = count;

            handles = new VkPhysicalDevice[device_count];

            if (device_count >= 1)
                handle = physicalDevicesptr[0];

            for (int i = 0; i < device_count; i++)
                handles[i] = physicalDevicesptr[i];

        }


        internal unsafe ReadOnlySpan<VkExtensionProperties> Instance_Extensions()
        {
            uint count = 0;
            vkEnumerateInstanceExtensionProperties(null, &count, null).CheckResult();

            ReadOnlySpan<VkExtensionProperties> properties = new VkExtensionProperties[count];
            fixed (VkExtensionProperties* ptr = properties)
            {
                vkEnumerateInstanceExtensionProperties(null, &count, ptr).CheckResult();
            }

            return properties;
        }



        internal VkExtensionProperties* Instance_ExtensionsPtr()
        {
            uint count = 0;

            vkEnumerateInstanceExtensionProperties(null, &count, null).CheckResult();
            instance_extensions_count = count;

            VkExtensionProperties* ext = stackalloc VkExtensionProperties[(int)count];
            vkEnumerateInstanceExtensionProperties(null, &count, ext).CheckResult();
            return ext;
        }


        internal VkFormat get_supported_depth_format(IEnumerable<VkFormat> depthFormats)
        {
            // Since all depth formats may be optional, we need to find a suitable depth format to use
            // Start with the highest precision packed format

            VkFormat depthFormat = VkFormat.Undefined;

            foreach (VkFormat format in depthFormats)
            {
                vkGetPhysicalDeviceFormatProperties(handle, format, out VkFormatProperties formatProps);

                // Format must support depth stencil attachment for optimal tiling
                if ((formatProps.optimalTilingFeatures & VkFormatFeatureFlags.DepthStencilAttachment) is not 0)
                {
                    depthFormat = format;
                }
            }



            return depthFormat;
        }

        internal string VendorNameString(uint vendorId)
        {
            switch (vendorId)
            {
                case 0x1002:
                    return "AMD";

                case 0x1010:
                    return "ImgTec";

                case 0x10DE:
                    return "NVIDIA";

                case 0x13B5:
                    return "ARM";

                case 0x5143:
                    return "Qualcomm";

                case 0x8086:
                    return "Intel";

                default:
                    return "Unknown";
            }
        }
    }
}
