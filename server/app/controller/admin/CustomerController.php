<?php
namespace app\controller\admin;

use app\model\Customer;

class CustomerController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');

        $query = Customer::order('id', 'desc');

        if ($keyword) {
            $query->where('name|company|phone|email', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'company' => input('company'),
            'phone' => input('phone'),
            'email' => input('email'),
            'address' => input('address'),
            'remark' => input('remark'),
            'status' => input('status', 1),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '客户名称不能为空');
        }

        $customer = Customer::create($data);
        return $this->success($customer, '创建成功');
    }

    public function edit($id)
    {
        $customer = Customer::find($id);
        if (!$customer) {
            return $this->error('not_found', '客户不存在');
        }

        $data = [];
        foreach (['name', 'company', 'phone', 'email', 'address', 'remark', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $customer->save($data);
        return $this->success($customer, '更新成功');
    }

    public function delete($id)
    {
        $customer = Customer::find($id);
        if (!$customer) {
            return $this->error('not_found', '客户不存在');
        }

        $customer->delete();
        return $this->success(null, '删除成功');
    }

    public function detail($id)
    {
        $customer = Customer::with(['workOrders', 'deployments'])->find($id);
        if (!$customer) {
            return $this->error('not_found', '客户不存在');
        }
        return $this->success($customer);
    }
}
