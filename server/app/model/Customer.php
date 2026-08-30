<?php
namespace app\model;

class Customer extends BaseModel
{
    protected $table = "zs_customers";

    public function orders()
    {
        return $this->hasMany(WorkOrder::class, "customer_id", "id");
    }
}

