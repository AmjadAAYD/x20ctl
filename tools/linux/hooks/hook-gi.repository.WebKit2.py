"""PyInstaller has no built-in WebKit2 hook; include both browser typelibs.

System GTK/WebKit libraries remain excluded by the Linux spec, while these
introspection descriptions travel with the frozen Python GI bindings.
"""
from PyInstaller.utils.hooks.gi import GiModuleInfo

binaries, datas, hiddenimports = [], [], []
for namespace in ("WebKit2", "JavaScriptCore"):
    module = GiModuleInfo(namespace, "4.1")
    native, descriptions, dependencies = module.collect_typelib_data()
    binaries += native
    datas += descriptions
    hiddenimports += dependencies
