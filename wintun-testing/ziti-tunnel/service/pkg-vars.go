package service

import (
	"github.com/michaelquigley/pfxlog"
)

var log = pfxlog.Logger()

const (
	StartEvent = 1
	ContinueEvent = 2
	PauseEvent = 3
	InstallEvent = 4
	InterrogateEvent = 10
	StopEvent = 100
	ShutdownEvent = 101
	ErrorEvent = 500

	// This is the name you will use for the NET START command
	SvcName = "ziti"

	// This is the name that will appear in the Services control panel
	SvcNameLong = SvcName + " long description here"
)