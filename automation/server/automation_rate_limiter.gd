class_name AutomationRateLimiter
extends RefCounted
## Token bucket. allow() consumes one token; refills continuously.

var capacity: float
var refill_per_second: float
var _tokens: float
var _last_msec: int


func _init(p_capacity: float = AutomationProtocol.RATE_BURST, p_refill: float = AutomationProtocol.RATE_PER_SECOND) -> void:
	capacity = p_capacity
	refill_per_second = p_refill
	_tokens = p_capacity
	_last_msec = Time.get_ticks_msec()


func allow() -> bool:
	var now: int = Time.get_ticks_msec()
	_tokens = minf(capacity, _tokens + (now - _last_msec) / 1000.0 * refill_per_second)
	_last_msec = now
	if _tokens < 1.0:
		return false
	_tokens -= 1.0
	return true
