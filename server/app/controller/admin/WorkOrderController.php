<?php
namespace app\controller\admin;

use app\model\WorkOrder;

class WorkOrderController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');
        $type = input('type');
        $priority = input('priority');
        $customerId = input('customer_id');

        $query = WorkOrder::order('id', 'desc');

        if ($keyword) {
            $query->where('fault_description|device_type|device_model|remark', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }
        if ($type) {
            $query->where('device_type', 'like', '%' . $type . '%');
        }
        if ($priority !== null && $priority !== '') {
            $query->where('priority', $priority);
        }
        if ($customerId) {
            $query->where('customer_id', $customerId);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $customerId = input('customer_id', 0, 'intval');
        if ($customerId <= 0) {
            $customerName = input('customer_name');
            $customerPhone = input('customer_phone');
            if ($customerName) {
                $customerData = [
                    'name' => $customerName,
                    'phone' => $customerPhone ?: '',
                    'created_by' => $this->userId,
                    'created_at' => date('Y-m-d H:i:s'),
                ];
                $customer = \think\facade\Db::name('customers')->insertGetId($customerData);
                $customerId = $customer;
            } else {
                return $this->error('param_error', '请选择客户');
            }
        }

        $deviceType = input('device_type');
        if (empty($deviceType)) {
            return $this->error('param_error', '设备类型不能为空');
        }

        $faultDescription = input('fault_description', input('title', ''));
        if (empty($faultDescription)) {
            return $this->error('param_error', '故障描述不能为空');
        }

        $orderNo = 'WO' . date('YmdHis') . str_pad(mt_rand(0, 9999), 4, '0', STR_PAD_LEFT);

        $data = [
            'order_no'          => $orderNo,
            'customer_id'       => $customerId,
            'device_type'       => $deviceType,
            'device_model'      => input('device_model', ''),
            'device_sn'         => input('device_sn', ''),
            'fault_description' => $faultDescription,
            'solution'          => input('solution', ''),
            'priority'          => input('priority', 'normal'),
            'charge_amount'     => input('charge_amount', 0),
            'remark'            => input('remark', ''),
            'created_by'        => $this->userId,
        ];

        $order = WorkOrder::create($data);
        return $this->success($order, '创建成功');
    }

    public function edit($id)
    {
        $order = WorkOrder::find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }

        $data = [];
        foreach (['device_type', 'device_model', 'device_sn', 'fault_description', 'solution', 'priority', 'charge_amount', 'remark', 'customer_id'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $order->save($data);
        return $this->success($order, '更新成功');
    }

    public function delete($id)
    {
        $order = WorkOrder::find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }

        $order->delete();
        return $this->success(null, '删除成功');
    }

    public function detail($id)
    {
        $order = WorkOrder::with(['customer', 'task'])->find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }
        return $this->success($order);
    }

    public function updateStatus($id)
    {
        $order = WorkOrder::find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }

        $status = input('status');
        $remark = input('remark');

        if (!$status) {
            return $this->error('param_error', '状态不能为空');
        }

        $order->status = $status;
        $order->remark = $remark;
        $order->save();

        return $this->success($order, '状态更新成功');
    }
}