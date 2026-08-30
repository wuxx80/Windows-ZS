<?php
namespace app\model;

class WorkOrder extends BaseModel
{
    protected $table = "zs_work_orders";

    public function customer()
    {
        return $this->belongsTo(Customer::class, "customer_id", "id");
    }

    public function task()
    {
        return $this->belongsTo(Task::class, "task_id", "id");
    }
}

