// Code generated by 'go generate'; DO NOT EDIT.

package firewall

import (
	"syscall"
	"unsafe"

	"golang.org/x/sys/windows"
)

var _ unsafe.Pointer

// Do the interface allocations only once for common
// Errno values.
const (
	errnoERROR_IO_PENDING = 997
)

var (
	errERROR_IO_PENDING error = syscall.Errno(errnoERROR_IO_PENDING)
)

// errnoErr returns common boxed Errno values, to prevent
// allocations at runtime.
func errnoErr(e syscall.Errno) error {
	switch e {
	case 0:
		return nil
	case errnoERROR_IO_PENDING:
		return errERROR_IO_PENDING
	}
	// TODO: add more here, after collecting data on the common
	// error values see on Windows. (perhaps when running
	// all.bat?)
	return e
}

var (
	modfwpuclnt = windows.NewLazySystemDLL("fwpuclnt.dll")

	procFwpmEngineOpen0           = modfwpuclnt.NewProc("FwpmEngineOpen0")
	procFwpmEngineClose0          = modfwpuclnt.NewProc("FwpmEngineClose0")
	procFwpmSubLayerAdd0          = modfwpuclnt.NewProc("FwpmSubLayerAdd0")
	procFwpmGetAppIdFromFileName0 = modfwpuclnt.NewProc("FwpmGetAppIdFromFileName0")
	procFwpmFreeMemory0           = modfwpuclnt.NewProc("FwpmFreeMemory0")
	procFwpmFilterAdd0            = modfwpuclnt.NewProc("FwpmFilterAdd0")
	procFwpmTransactionBegin0     = modfwpuclnt.NewProc("FwpmTransactionBegin0")
	procFwpmTransactionCommit0    = modfwpuclnt.NewProc("FwpmTransactionCommit0")
	procFwpmTransactionAbort0     = modfwpuclnt.NewProc("FwpmTransactionAbort0")
	procFwpmProviderAdd0          = modfwpuclnt.NewProc("FwpmProviderAdd0")
)

func fwpmEngineOpen0(serverName *uint16, authnService wtRpcCAuthN, authIdentity *uintptr, session *wtFwpmSession0, engineHandle unsafe.Pointer) (err error) {
	r1, _, e1 := syscall.Syscall6(procFwpmEngineOpen0.Addr(), 5, uintptr(unsafe.Pointer(serverName)), uintptr(authnService), uintptr(unsafe.Pointer(authIdentity)), uintptr(unsafe.Pointer(session)), uintptr(engineHandle), 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmEngineClose0(engineHandle uintptr) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmEngineClose0.Addr(), 1, uintptr(engineHandle), 0, 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmSubLayerAdd0(engineHandle uintptr, subLayer *wtFwpmSublayer0, sd uintptr) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmSubLayerAdd0.Addr(), 3, uintptr(engineHandle), uintptr(unsafe.Pointer(subLayer)), uintptr(sd))
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmGetAppIdFromFileName0(fileName *uint16, appID unsafe.Pointer) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmGetAppIdFromFileName0.Addr(), 2, uintptr(unsafe.Pointer(fileName)), uintptr(appID), 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmFreeMemory0(p unsafe.Pointer) {
	syscall.Syscall(procFwpmFreeMemory0.Addr(), 1, uintptr(p), 0, 0)
	return
}

func fwpmFilterAdd0(engineHandle uintptr, filter *wtFwpmFilter0, sd uintptr, id *uint64) (err error) {
	r1, _, e1 := syscall.Syscall6(procFwpmFilterAdd0.Addr(), 4, uintptr(engineHandle), uintptr(unsafe.Pointer(filter)), uintptr(sd), uintptr(unsafe.Pointer(id)), 0, 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmTransactionBegin0(engineHandle uintptr, flags uint32) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmTransactionBegin0.Addr(), 2, uintptr(engineHandle), uintptr(flags), 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmTransactionCommit0(engineHandle uintptr) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmTransactionCommit0.Addr(), 1, uintptr(engineHandle), 0, 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmTransactionAbort0(engineHandle uintptr) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmTransactionAbort0.Addr(), 1, uintptr(engineHandle), 0, 0)
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}

func fwpmProviderAdd0(engineHandle uintptr, provider *wtFwpmProvider0, sd uintptr) (err error) {
	r1, _, e1 := syscall.Syscall(procFwpmProviderAdd0.Addr(), 3, uintptr(engineHandle), uintptr(unsafe.Pointer(provider)), uintptr(sd))
	if r1 != 0 {
		if e1 != 0 {
			err = errnoErr(e1)
		} else {
			err = syscall.EINVAL
		}
	}
	return
}
